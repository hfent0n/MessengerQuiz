# Builds Messenger Quiz as one container: the .NET server, serving the built React client.

# 1. Build the client.
FROM node:22-slim AS client
WORKDIR /client
# Installing packages before copying the code means they're only reinstalled when they change.
COPY client/package.json client/package-lock.json ./
RUN npm ci
COPY client/ ./
RUN npm run build

# 2. Publish the server.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS server
WORKDIR /src
COPY server/MessengerQuiz.Api/MessengerQuiz.Api.csproj server/MessengerQuiz.Api/
RUN dotnet restore server/MessengerQuiz.Api/MessengerQuiz.Api.csproj
COPY server/MessengerQuiz.Api/ server/MessengerQuiz.Api/
RUN dotnet publish server/MessengerQuiz.Api/MessengerQuiz.Api.csproj -c Release -o /app --no-restore

# 3. Run. The chiseled image has no shell or package manager and runs as a non-root user. The extra variant
# includes ICU, which player names need for Unicode normalisation.
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra
WORKDIR /app
COPY --from=server /app ./
COPY --from=client /client/dist ./wwwroot

# Cloud Run passes the port in PORT. This is the port used when nothing passes one.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "MessengerQuiz.Api.dll"]
