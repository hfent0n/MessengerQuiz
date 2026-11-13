# Deploying to Google Cloud Run

This guide puts Messenger Quiz on the internet with Google Cloud Run. It stays within Cloud Run's free monthly allowance for a portfolio site, and a billing cap turns everything off if spending ever reaches a limit you set.

Chats are **encrypted in the player's browser** before upload, so the hosted server can't read messages or names. The [main README's privacy section](../README.md#privacy) explains what it can see.

## How it runs

- **One container.** The [Dockerfile](../Dockerfile) builds the React client and serves it from the .NET server, so the whole app is one service.
- **One instance at most.** Chats and multiplayer games live in the server's memory, so a second copy would split them up. It scales to zero when nobody is using it, which also wipes that memory.
- **Configured with environment variables.** `Quiz__TrustForwardedHeaders` reads visitors' addresses from Cloud Run's proxy, so the per-visitor rate limits work. The default limits are sized for a 512 MB instance.

## Cost

Cloud Run includes a free allowance of CPU time, memory and requests every month, and a portfolio site should stay well inside it. Building the image uses Cloud Build, which has its own free allowance of build minutes. Check [Google's pricing page](https://cloud.google.com/run/pricing) for the current figures.

What keeps costs low and bounded:

- **Maximum instances: 1.** There can never be more than one copy running.
- **Minimum instances: 0.** Nothing runs, or costs, while nobody is visiting.
- **CPU charged only during requests.** An idle instance costs nothing.
- **The billing cap** (step 5), which turns billing off once this month's spending reaches your budget.

## One-time setup

### 1. Install and sign in

Every command in this guide is a single line, so paste each one whole.

Install the [Google Cloud CLI](https://cloud.google.com/sdk/docs/install), then sign in:

```bash
gcloud auth login
```

Set your project and a region. Run these in each new terminal before the other commands, because they set the variables the commands use. The live site uses `europe-west2` (London). Run `gcloud run regions list` to see the others. If you use a different project or region, change the values at the top of [`deploy.yml`](../.github/workflows/deploy.yml) to match.

```bash
export PROJECT_ID=your-project-id
export REGION=europe-west2
gcloud config set project $PROJECT_ID
gcloud config set run/region $REGION
```

Your project needs a billing account linked to it, even to use the free allowance. Do that in the Google Cloud console under **Billing**.

### 2. Turn on the services it uses

```bash
gcloud services enable run.googleapis.com cloudbuild.googleapis.com artifactregistry.googleapis.com cloudfunctions.googleapis.com eventarc.googleapis.com pubsub.googleapis.com cloudbilling.googleapis.com billingbudgets.googleapis.com
```

### 3. Create an identity for the app

The app runs as its own service account with no permissions, since it doesn't use any other Google Cloud service. Otherwise it would run as the default account, which can change almost anything in the project.

```bash
gcloud iam service-accounts create messenger-quiz-run --display-name "Messenger Quiz app"
```

### 4. Deploy

Run this from the repository's root folder:

```bash
gcloud run deploy messenger-quiz --source . --service-account messenger-quiz-run@$PROJECT_ID.iam.gserviceaccount.com --allow-unauthenticated --max-instances 1 --min-instances 0 --cpu 1 --memory 512Mi --timeout 3600 --set-env-vars Quiz__TrustForwardedHeaders=true
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
gcloud artifacts repositories set-cleanup-policies cloud-run-source-deploy --location $REGION --policy deploy/cleanup-policy.json
```

Once step 6 is set up, GitHub deploys every change to `main` for you. Until then, run the `gcloud run deploy` command again to deploy a new version.

### 5. Set up the billing cap

Google Cloud budgets only send alerts. To make one a cap:

1. **The budget publishes an alert** to a Pub/Sub topic, Google's messaging service, several times a day.
2. **A small function reads each alert.** Once spending reaches the budget, it turns off billing for the project, which stops everything in it. The function is in [`billing-cap`](billing-cap).

The function needs its own identity, allowed only to link and unlink this project's billing:

```bash
gcloud iam service-accounts create billing-cap --display-name "Billing cap"
gcloud projects add-iam-policy-binding $PROJECT_ID --member serviceAccount:billing-cap@$PROJECT_ID.iam.gserviceaccount.com --role roles/billing.projectManager
```

Create the topic and deploy the function to listen to it:

```bash
gcloud pubsub topics create billing-cap

gcloud functions deploy billing-cap --gen2 --region $REGION --runtime nodejs22 --source deploy/billing-cap --entry-point stopBilling --trigger-topic billing-cap --service-account billing-cap@$PROJECT_ID.iam.gserviceaccount.com --trigger-service-account billing-cap@$PROJECT_ID.iam.gserviceaccount.com --set-env-vars PROJECT_ID=$PROJECT_ID --max-instances 1 --memory 256Mi --no-allow-unauthenticated

gcloud functions add-invoker-policy-binding billing-cap --region $REGION --member serviceAccount:billing-cap@$PROJECT_ID.iam.gserviceaccount.com
```

The last command lets Pub/Sub, using the function's identity, run the function. Nobody else can.

Create the budget. The amount's currency must match your billing account's, such as `5GBP` or `5USD`. It also sends emails at 50%, 90% and 100%:

```bash
export BILLING_ACCOUNT=$(gcloud billing projects describe $PROJECT_ID --format 'value(billingAccountName)' | sed 's|billingAccounts/||')

gcloud billing budgets create --billing-account $BILLING_ACCOUNT --display-name "Messenger Quiz cap" --budget-amount 5GBP --filter-projects projects/$PROJECT_ID --threshold-rule percent=0.5 --threshold-rule percent=0.9 --threshold-rule percent=1.0 --notifications-rule-pubsub-topic projects/$PROJECT_ID/topics/billing-cap
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

### 6. Set up automatic deploys

Two GitHub Actions workflows build, test and deploy the app:

- **[`ci.yml`](../.github/workflows/ci.yml)** runs on every pull request and every push to `main`. It runs the server tests, the client's lint, tests and build, the billing cap's tests, and a Docker build.
- **[`deploy.yml`](../.github/workflows/deploy.yml)** runs after CI passes on `main`. It does these steps:
  1. Builds the image and pushes it to Artifact Registry, tagged with the commit.
  2. Deploys it to Cloud Run with the same settings as step 4.
  3. Waits until `/api/health` reports the new commit. It can also be run by hand from the repository's **Actions** tab.

GitHub signs in to Google with **Workload Identity Federation**, so there's no key to store or leak:

1. Each run asks GitHub for a short-lived token that says which repository and branch it's running from.
2. Google checks that token against the rules below and gives back a Google token that lasts about an hour.
3. The run uses that token to act as a deploy-only service account.

Turn on the two services that do the token exchange:

```bash
gcloud services enable iamcredentials.googleapis.com sts.googleapis.com
```

Create the deploy account, and let it do only three things:

- deploy new versions of Cloud Run services
- run them as the app's own account
- push images to the one image repository

```bash
gcloud iam service-accounts create github-deployer --display-name "GitHub Actions deployer"

gcloud projects add-iam-policy-binding $PROJECT_ID --member serviceAccount:github-deployer@$PROJECT_ID.iam.gserviceaccount.com --role roles/run.developer

gcloud iam service-accounts add-iam-policy-binding messenger-quiz-run@$PROJECT_ID.iam.gserviceaccount.com --member serviceAccount:github-deployer@$PROJECT_ID.iam.gserviceaccount.com --role roles/iam.serviceAccountUser

gcloud artifacts repositories add-iam-policy-binding cloud-run-source-deploy --location $REGION --member serviceAccount:github-deployer@$PROJECT_ID.iam.gserviceaccount.com --role roles/artifactregistry.writer
```

Create a pool for GitHub's tokens and a provider that only accepts tokens from this repository's `main` branch. `1443354851` is the repository's ID on GitHub. The provider checks the ID rather than the name, because a name can be reused if the repository is renamed or deleted.

```bash
gcloud iam workload-identity-pools create github --location global --display-name "GitHub Actions"

gcloud iam workload-identity-pools providers create-oidc github --location global --workload-identity-pool github --display-name "GitHub" --issuer-uri https://token.actions.githubusercontent.com --attribute-mapping "google.subject=assertion.sub,attribute.repository_id=assertion.repository_id,attribute.ref=assertion.ref" --attribute-condition "assertion.repository_id == '1443354851' && assertion.ref == 'refs/heads/main'"
```

Let runs from this repository act as the deploy account:

```bash
export PROJECT_NUMBER=$(gcloud projects describe $PROJECT_ID --format 'value(projectNumber)')

gcloud iam service-accounts add-iam-policy-binding github-deployer@$PROJECT_ID.iam.gserviceaccount.com --role roles/iam.workloadIdentityUser --member "principalSet://iam.googleapis.com/projects/$PROJECT_NUMBER/locations/global/workloadIdentityPools/github/attribute.repository_id/1443354851"
```

New permissions can take a few minutes to apply. After that, push to `main` and watch the **Actions** tab: CI runs first, then Deploy.

Optionally, stop changes reaching `main` without passing CI. On GitHub, open the repository's **Settings**, then **Rules**, then **Rulesets**. Add a rule for `main` that requires a pull request and the four CI checks to pass.

## Changing settings

Any setting in [`QuizOptions`](../server/MessengerQuiz.Api/QuizOptions.cs) can be changed with an environment variable named `Quiz__` followed by the setting's name. For example, `Quiz__QuestionsPerGame=5`.

Add it to `--set-env-vars` in [`deploy.yml`](../.github/workflows/deploy.yml). Each deploy replaces the service's environment variables with that list, so a setting changed only in the console or with `gcloud run services update` is lost at the next deploy.
