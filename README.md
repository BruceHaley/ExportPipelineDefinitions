# Export Azure DevOps Pipeline Definitions
This command line app exports all Azure DevOps pipeline definitions to .json and .yml files. It finds all the pipelines to which you have access in your organization. It organizes them in a folder tree on disk. 

**Benefits:** 
- You can key-word search all your build definitions at once. Just use "Find in Files" in Visual Studio, VS Code, or similar.
- You can save a snapshot of all your pipelines as they are now for future reference. 

Note: The exported .json files are not in a format that can be imported through the Azure DevOps UI.

## Quickstart
1. Go to [releases](releases). Click-to-download the .exe, .config, and .dll files. Put them together in a folder. 
1. Edit the .config file and set the three values: personalAccessToken, organization, and outputPath.
   For detailed instructions, read starting on line 30 in [ExportPipelineDefinitions/Program.cs](ExportPipelineDefinitions/Program.cs).
1. Run the .exe.
