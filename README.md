# Export Azure DevOps Pipeline Definitions
This app traverses all your Azure DevOps pipeline definitions in all projects and saves them to your local drive. This makes managing large numbers of pipelines easier by listing them and making them searchable.

It finds all the pipelines to which you have access in your organization. 
- It saves classic pipelines as .json files and yaml pipelines as .yml files, organized in a hierarchical folder tree. 
- It generates a list of all pipelines in Markdown format. 
- It writes the pipeline names and their projects to a spreadsheet. 

**Benefits:** 
- This lets you key-word search globally all pipelines across all projects using "Find in Files" in Visual Studio, VS Code, or other search tool.
- You can build a pipeline history archive by running this app periodically and saving the results. 

If you manage a large number of ADO pipelines, **you need this app.**

Note: The exported .json files are not in a format that can be imported using the Azure DevOps UI.

## Quickstart
Prerequirements:
1. Create PAT token with following scopes:
* Build: Read
* Code: Read
* Project and Team: Read
* Release: Read

### On Windows
1. Go to [releases](../../releases). Click-to-download the .zip file and extract it to folder.
2. Start PowerShell and run commands:
```powershell
$env:personalAccessToken = "<PAT token>"
$env:organization = "<Azure DevOps organization name>"
$env:outputPath = "C:\temp\PipelineDefinitions\"
.\ExportPipelineDefinitions.exe
```

### On Linux container
1. Clone this GIT repository
2. Build container image `docker build . -t export-pipeline-definitions`
3. Run tool as container:
```bash
mkdir output
docker run -it --rm \
	--user $(id -u):$(id -g) \
	--cap-drop ALL \
	--read-only \
	-e personalAccessToken="<PAT token>" \
	-e organization="<Azure DevOps organization name>" \
	-v $(pwd)/output:/output \
	export-pipeline-definitions
```
