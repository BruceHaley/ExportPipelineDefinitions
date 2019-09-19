# Export Azure DevOps Pipeline Definitions
This command line app exports all Azure DevOps pipeline definitions to .json files. It finds all the pipelines to which you have access in your organization. It organizes them in a folder tree on disk. 

**Benefits:** 
- Puts your pipelines in searchable files.
- Lets you collect snapshots of your pipeline history. 

**Note:** 
The exported files are complete. However, the format does not support importing them through the Azure DevOps UI to create new definitions.

## Quickstart
1. Clone this repository.
1. Open the solution in Visual Studio.
1. In Solution Explorer open Properties/Settings.settings and fill in the three values: personal access token, organization, and output path.
   For details, read the comments from line 25 in Program.cs.
1. Hit F5 to run.
