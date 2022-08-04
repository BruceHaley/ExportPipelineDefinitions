# Azure DevOps Pipelines Indexer
This app reads Azure DevOps pipeline definitions in all your projects and writes them to a searchable folder structure on your local drive.

## Benefits:
- You can key-word search all pipelines across all projects at once. 
- You can build a pipeline history archive by running this app periodically. 
- Creates a spreadsheet with pipeline names and their projects. 
- Lists pipeline call hierarchies to the console in Markdown format. 

## How to search:

From the root folder, use "Find in Files" in Visual Studio, VS Code, or other text editor.

## How it works:

The app finds all pipelines in all projects to which you have access in your organization. 
It saves classic pipelines as .json files and yaml pipelines as .yml files organized hierarchically in a folder tree on your local drive. 
(Note: The .json files are not importable using [Import a pipeline](https://docs.microsoft.com/en-us/azure/devops/pipelines/get-started/clone-import-pipeline?view=azure-devops&tabs=classic#export-and-import-a-pipeline) in ADO.)

If you manage a large number of ADO pipelines, **you need this app.**

## Quickstart
1. Go to [releases](../../releases). Click-to-download the .exe, .config, and .dll files. Put them together in a folder. 
1. Edit the .config file and set the four values as needed: azurePersonalAccessToken, githubPersonalAccessToken, organization, and outputPath.
   See description in [ExportPipelineDefinitions/Program.cs](https://github.com/BruceHaley/ExportPipelineDefinitions/blob/master/ExportPipelineDefinitions/Program.cs#L28) starting on line 28.
1. Run ExportPipelineDefinitions.exe.
