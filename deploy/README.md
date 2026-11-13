# Deploying to Google Cloud Run

This guide puts Messenger Quiz on the internet with Google Cloud Run. It stays within Cloud Run's free monthly allowance for a portfolio site, and a billing cap turns everything off if spending ever reaches a limit you set.

The hosted site **only plays the demo chat**. Real chats can't be uploaded to it, so it never holds anyone's messages. To play your own chats, run Messenger Quiz on your own computer.

## How it runs

- **One container.** The [Dockerfile](../Dockerfile) builds the React client and serves it from the .NET server, so the whole app is one service.
- **One instance at most.** Chats and multiplayer games live in the server's memory, so a second copy would split them up. It scales to zero when nobody is using it, which also wipes that memory.
- **Configured with environment variables.** `Quiz__DemoOnly` turns uploads off. The Docker image turns it on by default, and the deploy command sets it again. `Quiz__TrustForwardedHeaders` reads visitors' addresses from Cloud Run's proxy, so the per-visitor rate limits work.

## Cost

Cloud Run includes a free allowance of CPU time, memory and requests every month, and a portfolio site should stay well inside it. Building the image uses Cloud Build, which has its own free allowance of build minutes. Check [Google's pricing page](https://cloud.google.com/run/pricing) for the current figures.

What keeps costs low and bounded:

- **Maximum instances: 1.** There can never be more than one copy running.
- **Minimum instances: 0.** Nothing runs, or costs, while nobody is visiting.
- **CPU charged only during requests.** An idle instance costs nothing.
- **The billing cap** (step 5), which turns billing off once this month's spending reaches your budget.

## One-time setup

### 1. Install and sign in

Install the [Google Cloud CLI](https://cloud.google.com/sdk/docs/install), then sign in:

```bash
gcloud auth login
```

Set your project and a region. Run these in each new terminal before the other commands, because they set the variables the commands use. `europe-west1` (Belgium) has some of the lowest prices in Europe. Run `gcloud run regions list` to see the others.

```bash
export PROJECT_ID=your-project-id
export REGION=europe-west1
gcloud config set project $PROJECT_ID
gcloud config set run/region $REGION
```

Your project needs a billing account linked to it, even to use the free allowance. Do that in the Google Cloud console under **Billing**.

### 2. Turn on the services it uses

```bash
gcloud services enable run.googleapis.com cloudbuild.googleapis.com artifactregistry.googleapis.com \
  cloudfunctions.googleapis.com eventarc.googleapis.com pubsub.googleapis.com \
  cloudbilling.googleapis.com billingbudgets.googleapis.com
```

### 3. Create an identity for the app

The app runs as its own service account with no permissions, since it doesn't use any other Google Cloud service. Otherwise it would run as the default account, which can change almost anything in the project.

```bash
gcloud iam service-accounts create messenger-quiz-run --display-name "Messenger Quiz app"
```

### 4. Deploy

Run this from the repository's root folder:

```bash
gcloud run deploy messenger-quiz \
  --source . \
  --service-account messenger-quiz-run@$PROJECT_ID.iam.gserviceaccount.com \
  --allow-unauthenticated \
  --max-instances 1 \
  --min-instances 0 \
  --cpu 1 \
  --memory 512Mi \
  --timeout 3600 \
  --set-env-vars Quiz__DemoOnly=true,Quiz__TrustForwardedHeaders=true
```

What this does:

1. **`--source .`** uploads the folder to Cloud Build. [`.gcloudignore`](../.gcloudignore) leaves out Facebook exports, packages and build output. Cloud Build then builds the image from the Dockerfile and stores it in Artifact Registry, Google's image store.
2. **`--allow-unauthenticated`** makes the site public.
3. **`--max-instances 1`** and **`--min-instances 0`** allow one copy at most, and none when it's idle.
4. **`--timeout 3600`** keeps a multiplayer game's connection open for up to an hour. Each WebSocket counts as one long request.

The first deploy asks to create an Artifact Registry repository called `cloud-run-source-deploy`. Say yes. It prints the site's address when it's done.

Check the new version is up, using the address it printed:

```bash
curl https://YOUR-SERVICE-URL/api/health
```

Each deploy stores a new image. This keeps only the latest 3, so storage stays within the free allowance:

```bash
gcloud artifacts repositories set-cleanup-policies cloud-run-source-deploy \
  --location $REGION --policy deploy/cleanup-policy.json
```

To deploy a new version later, run the `gcloud run deploy` command again.

### 5. Set up the billing cap

Google Cloud budgets only send alerts. To make one a cap:

1. **The budget publishes an alert** to a Pub/Sub topic, Google's messaging service, several times a day.
2. **A small function reads each alert.** Once spending reaches the budget, it turns off billing for the project, which stops everything in it. The function is in [`billing-cap`](billing-cap).

The function needs its own identity, allowed only to link and unlink this project's billing:

```bash
gcloud iam service-accounts create billing-cap --display-name "Billing cap"
gcloud projects add-iam-policy-binding $PROJECT_ID \
  --member serviceAccount:billing-cap@$PROJECT_ID.iam.gserviceaccount.com \
  --role roles/billing.projectManager
```

Create the topic and deploy the function to listen to it:

```bash
gcloud pubsub topics create billing-cap

gcloud functions deploy billing-cap \
  --gen2 \
  --region $REGION \
  --runtime nodejs22 \
  --source deploy/billing-cap \
  --entry-point stopBilling \
  --trigger-topic billing-cap \
  --service-account billing-cap@$PROJECT_ID.iam.gserviceaccount.com \
  --trigger-service-account billing-cap@$PROJECT_ID.iam.gserviceaccount.com \
  --set-env-vars PROJECT_ID=$PROJECT_ID \
  --max-instances 1 \
  --memory 256Mi \
  --no-allow-unauthenticated

gcloud functions add-invoker-policy-binding billing-cap --region $REGION \
  --member serviceAccount:billing-cap@$PROJECT_ID.iam.gserviceaccount.com
```

The last command lets Pub/Sub, using the function's identity, run the function. Nobody else can.

Create the budget. The amount's currency must match your billing account's, such as `5GBP` or `5USD`. It also sends emails at 50%, 90% and 100%:

```bash
export BILLING_ACCOUNT=$(gcloud billing projects describe $PROJECT_ID \
  --format 'value(billingAccountName)' | sed 's|billingAccounts/||')

gcloud billing budgets create \
  --billing-account $BILLING_ACCOUNT \
  --display-name "Messenger Quiz cap" \
  --budget-amount 5GBP \
  --filter-projects projects/$PROJECT_ID \
  --threshold-rule percent=0.5 \
  --threshold-rule percent=0.9 \
  --threshold-rule percent=1.0 \
  --notifications-rule-pubsub-topic projects/$PROJECT_ID/topics/billing-cap
```

Check the function receives alerts by sending a pretend one that's under budget. It's harmless:

```bash
gcloud pubsub topics publish billing-cap --message '{"costAmount": 1, "budgetAmount": 5, "currencyCode": "GBP"}'
gcloud functions logs read billing-cap --region $REGION --limit 5
```

The logs should say `Under budget: 1 of 5 GBP.`

Things to know about the cap:

- **It can overshoot a little.** Google's billing figures lag by up to several hours, so spending can pass the budget before the cap acts.
- **The site stays off until you turn billing back on.** Do that in the console, or with:

  ```bash
  gcloud billing projects link $PROJECT_ID --billing-account $BILLING_ACCOUNT
  ```

- **Keep this project for Messenger Quiz only.** If billing stays off for a long time, Google can delete the project's resources. The app keeps nothing, so you'd only need to deploy it again.

## Changing settings

Any setting in [`QuizOptions`](../server/MessengerQuiz.Api/QuizOptions.cs) can be changed with an environment variable named `Quiz__` followed by the setting's name. For example, `Quiz__QuestionsPerGame=5`. Change one without a new build like this:

```bash
gcloud run services update messenger-quiz --update-env-vars Quiz__QuestionsPerGame=5
```

Don't turn `Quiz__DemoOnly` off on the hosted site. The site would then accept and hold people's real chats, and the privacy promises in the main README only hold when it runs on your own computer.
