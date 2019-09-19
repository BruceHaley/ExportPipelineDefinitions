# Export Azure DevOps Pipeline Definitions
This command line app exports all Azure DevOps pipeline definitions to .json files. It finds all the pipelines to which you have access in your organization. It organizes them in a folder tree on disk. 

**Benefits:** 
- Puts your pipelines in searchable files.
- Lets you collect snapshots of your pipeline history. 

**Note:** 
The exported files are complete. However, the format does not support importing them through the Azure DevOps UI to create new definitions.

## Quickstart
1. Go to releases and click-to-download the 3 files, .exe, .config, and .dll. Put them in a single folder. 
1. Edit file ExportPipelineDefinitions.exe.config and set the three values: personalAccessToken, organization, and outputPath.
   For details, read the comments from line 25 in ExportPipelineDefinitions\Program.cs.
1. Run the .exe by double-clicking, or run it from the command line.
