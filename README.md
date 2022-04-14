# Export Azure DevOps Pipeline Definitions
This app traverses all your Azure DevOps pipeline definitions and saves them to a folder on your local drive. It essentially builds a searchable index.

It finds all the pipelines to which you have access in your organization. It organizes them as .json and .yml files in a folder tree. It lists pipeline names to the console in Markdown format and puts them in a spreadsheet file. 

**Benefits:** 
- If you manage a large number of ADO pipelines, **you need this app.**
- This lets you key-word search all pipelines at once. Do it using "Find in Files" in Visual Studio or VS Code or similar.
- By running this app periodically and saving the results, you can create a searchable historical pipeline archive. 

Note: The exported .json files are in a format that cannot be imported using the Azure DevOps UI.

## Quickstart
1. Go to [releases](../../releases). Click-to-download the .exe, .config, and .dll files. Put them together in a folder. 
1. Edit the .config file and set the three values: personalAccessToken, organization, and outputPath.
   For more detailed instructions, read from line 30 in [ExportPipelineDefinitions/Program.cs](https://github.com/BruceHaley/ExportPipelineDefinitions/blob/51792ed245a4c62cadb4707ed62960c6d959102f/ExportPipelineDefinitions/Program.cs#L30).
1. Run the .exe.
