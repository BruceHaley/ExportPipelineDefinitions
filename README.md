# Export Azure DevOps Pipeline Definitions
This command line app exports all Azure DevOps pipeline definitions to .json and .yml files. It finds all the pipelines to which you have access in your organization. It organizes them in a folder tree on disk. 

**Benefits:** 
- Lets you search all your build definitions at once using Visual Studio.
- Lets you save snapshots of your pipeline history. 

**Note:** 
The exported .json files format does not support importing them through the Azure DevOps UI to create new definitions.

## Quickstart
1. Go to releases and click-to-download the 3 files, .exe, .config, and .dll. Put them together in a folder. 
1. Edit file ExportPipelineDefinitions.exe.config and set the three values: personalAccessToken, organization, and outputPath.
   For details, read "Before running this program..." starting on line 28 in ExportPipelineDefinitions\Program.cs.
1. Run the .exe.
