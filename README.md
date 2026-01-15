# Persistent Volume & GHCR Publish Sample

This sample is based on the aspire-samples/volumemount sample. It demonstrates how to configure a SQL Server container to use a persistent volume in Aspire, so that the data is persisted across app launches. It shows how to write files (image uploads) to a persistent volume from a Blazor Web app.

In addition to this, it also shows how to publish the docker-compose artifacts and push the BlazorWeb image to GitHub Container Registry from a GitHub Actions workflow.
