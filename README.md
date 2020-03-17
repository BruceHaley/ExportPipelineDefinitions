# Export Azure DevOps Pipeline Definitions
This command line app exports all Azure DevOps pipeline definitions to .json and .yml files. It finds all the pipelines to which you have access in your organization. It organizes them in a folder tree on disk. 

**Benefits:** 
- Lets you search all your build definitions at once using "Find in Files" in Visual Studio, VS Code, or similar.
- Lets you save snapshots of your pipeline history. 

**Note:** 
The exported .json files format is not importable through the Azure DevOps UI to create new definitions.

## Quickstart
1. Go to releases and click-to-download the .exe, .config, and .dll files. Put them together in a folder. 
1. Edit the .config file and set the three values: personalAccessToken, organization, and outputPath.
   For detailed instructions, read starting on line 30 in [ExportPipelineDefinitions/Program.cs](ExportPipelineDefinitions/Program.cs).
1. Run the .exe.
