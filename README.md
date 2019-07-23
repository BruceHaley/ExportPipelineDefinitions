# ExportPipelineDefinitions
This app exports all Azure DevOps pipeline definitions to .json files in a single command. It iterates through all projects to which you have access in your organization. 

Note: Though the exported files are complete, the format is such that Azure DevOps UI will not import them to create new definitions.

## Quickstart
1. Clone this repository.
1. Open the solution in Visual Studio.
1. In Solution Explorer open Properties/Settings.settings and fill in the three values.
   For hints, read the comments from line 25 in Program.cs.
1. Hit F5 to run.
