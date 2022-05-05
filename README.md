# Export Azure DevOps Pipeline Definitions
This app traverses all your Azure DevOps pipeline definitions in all projects and saves them to your local drive. This makes managing large numbers of pipelines easier by listing them and making them searchable.

It finds all the pipelines to which you have access in your organization. 
- It saves classic pipelines as .json files and yaml pipelines as .yml files, organized in a hierarchical folder tree. 
- It generates a list of all pipelines in Markdown format. 
- It writes the pipeline names and their projects to a spreadsheet. 

**Benefits:** 
- This lets you key-word search globally all pipelines across all projects using "Find in Files" in Visual Studio, VS Code, or other search tool.
- If you run this app periodically and save the results, you can build a pipeline history archive. 
- If you manage a large number of ADO pipelines, **you need this app.**

Note: The exported .json files are not in a format that can be imported using the Azure DevOps UI.

## Quickstart
1. Go to [releases](../../releases). Click-to-download the .exe, .config, and .dll files. Put them together in a folder. 
1. Edit the .config file and set the three values: personalAccessToken, organization, and outputPath.
   See details in [ExportPipelineDefinitions/Program.cs](https://github.com/BruceHaley/ExportPipelineDefinitions/blob/51792ed245a4c62cadb4707ed62960c6d959102f/ExportPipelineDefinitions/Program.cs#L30), lines 30 - 32.
1. Run ExportPipelineDefinitions.exe.
