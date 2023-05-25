using Newtonsoft.Json.Linq;
//using Json;
using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Net;

namespace ExportPipelineDefinitions
{
    /// <summary>
    /// Exports all Azure DevOps build and release pipeline definitions in your organization to disk.
    /// </summary>
    /// <remarks>
    /// Writes .json  and .yml files to a folder hierarchy mirroring the Azure DevOps hierarchy.
    /// This lets you search all your build definitions at once using Visual Studio.
    /// 
    /// Note: I tried importing the .json files via the Azure DevOps UI to create new definitions.
    /// However, the import fails, as their format does not exactly match what the UI requires.
    /// </remarks>
    class Program
    {
        // Before running this program, set up the following vars in file Properties\Settings.settings.
        //
        // azurePersonalAccessToken description: https://docs.microsoft.com/en-us/azure/devops/integrate/get-started/authentication/pats?view=azure-devops
        // githubPersonalAccessToken (optional) is used to download .yml pipelines files from private Github repos. How to create yours:
        //    https://help.github.com/en/github/authenticating-to-github/creating-a-personal-access-token-for-the-command-line
        // organization is the value in your Azure DevOps URL: https://dev.azure.com/{yourorganization}
        // outputPath defines where the .json files will be written. Requires a trailing backslash. Example: @"C:\temp\PipelineDefinitions\"

        static string azurePersonalAccessToken;
        static string githubPersonalAccessToken;
        static string organization;
        static string outputPath;

        const string buildDomain = "dev.azure.com";
        const string releaseDomain = "vsrm.dev.azure.com";
        static string domain = buildDomain;
        static bool isYamlPipeline = false;
        static List<string> yamlFileNames = new List<string>();
        // For holding duplicate template file names found in yamlFileNames. Allows avoiding file overwrites.
        static HashSet<string> yamlFileNameDuplicates = new HashSet<string>();

        public class BuildDef
        {
            public int id;
            public string name;
            public string path;
        }

        static List<BuildDef> definitionList = new List<BuildDef>();

        public class Proj
        {
            public string id;
            public string name;
        }

        static List<Proj> projectList = new List<Proj>();

        static void Main(string[] args)
        {
            try
            {
                GetSettings();
                PopulateProjectList().Wait();
                if (Directory.Exists(outputPath))
                {
                    Console.WriteLine("\nDeleting the output folder " + outputPath + ". \nPress any key to continue (Ctrl+C to abort)");
                    Console.ReadKey();
                    Directory.Delete(outputPath, true);
                }
                Console.WriteLine("Writing definition files to: " + outputPath);
                BuildCsvString(1, "Project,Type,Definition Name,Yaml Call,Yaml Subcall");

                foreach (Proj proj in projectList)
                {
                    Console.WriteLine("\n### " + proj.name + " Project");
                    BuildCsvString(1, proj.name);

                    definitionList.Clear();
                    domain = buildDomain;
                    PopulateBuildDefinitionList(proj.name).Wait();
                    if (definitionList.Count > 0)
                    {
                        Console.WriteLine("  * #### builds");
                        BuildCsvString(2, "builds");
                    }
                    foreach (BuildDef buildDef in definitionList)
                    {
                        WriteDefinitionToFile(proj.name, "build", buildDef).Wait();
                    }

                    definitionList.Clear();
                    domain = releaseDomain;
                    GetReleaseDefinitions(proj.name).Wait();
                    if (definitionList.Count > 0)
                    {
                        Console.WriteLine("  * #### releases");
                        BuildCsvString(2, "releases");
                    }
                    foreach (BuildDef buildDef in definitionList)
                    {
                        WriteDefinitionToFile(proj.name, "release", buildDef).Wait();
                    }
                }
                Console.WriteLine("\n'**' = File not found. Possible authentication, repo, or file name issue.\n");
                Console.WriteLine("Finished writing definition files to: " + outputPath + "\n");
            }
            catch (Exception ex)
            {
                if (ex.InnerException != null)
                {
                    Console.WriteLine(ex.InnerException.Message.ToString());
                }
                else
                {
                    Console.WriteLine(ex.Message.ToString());
                }
            }

            // Write .csv file containing all build definitions.
            if (Directory.Exists(outputPath))
            {
                BuildCsvString(1, "Writing to column 1 flushes the csvColumns buffer to csvString");
                File.WriteAllText(outputPath + @"\BuildDefinitions.csv", csvString);
            }

            Console.WriteLine("Done. Press any key");
            Console.ReadKey();
        }

        public static void GetSettings()
        {
            azurePersonalAccessToken = Properties.Settings.Default.azurePersonalAccessToken;
            githubPersonalAccessToken = Properties.Settings.Default.githubPersonalAccessToken;
            organization = Properties.Settings.Default.organization;
            outputPath = Properties.Settings.Default.outputPath;
            outputPath = outputPath.TrimEnd('\\') + DateTime.Now.ToString("_yyyy-MM-dd") + "\\";

            ValidateSettingValue(nameof(azurePersonalAccessToken), azurePersonalAccessToken);
            // Not validating githubPersonalAccessToken because it is not required.
            ValidateSettingValue(nameof(organization), organization);
            ValidateSettingValue(nameof(outputPath), outputPath);
        }

        public static void ValidateSettingValue(string nameOfVar, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidDataException(
                    string.Format(
                        "Invalid {0} value in file {1}.config.",
                        nameOfVar,
                        System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName));
            }
        }

        public static async Task PopulateProjectList()
        {
            // Documentation: https://docs.microsoft.com/en-us/rest/api/azure/devops/?view=azure-devops-server-rest-5.0
            //    https://docs.microsoft.com/en-us/rest/api/azure/devops/core/projects/list?view=azure-devops-rest-5.1

            string restUrl = string.Format("https://{0}/{1}/_apis/projects", domain, organization);
            Console.WriteLine("Getting build definitions from " + restUrl + "\n");

            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Add(
                    new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
                    Convert.ToBase64String(
                        System.Text.ASCIIEncoding.ASCII.GetBytes(
                            string.Format("{0}:{1}", "", azurePersonalAccessToken))));

                using (HttpResponseMessage response = await client.GetAsync(restUrl))
                {
                    response.EnsureSuccessStatusCode();
                    string responseBody = await response.Content.ReadAsStringAsync();
                    if (responseBody.Contains("!DOCTYPE"))
                    {
                        throw new AccessViolationException(
                            string.Format(
                                "Cannot access Azure. Please ensure azurePersonalAccessToken and organization values are correct in file {0}.config.",
                                System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName));
                    }
                    JObject json = JObject.Parse(responseBody);
                    foreach (JObject o in json.Last.First)
                    {
                        Proj proj = new Proj();
                        proj.id = o["id"].ToString();
                        proj.name = o["name"].ToString();
                        projectList.Add(proj);
                    }
                }
            }
            projectList.Sort(CompareProjects);
        }

        public static async Task PopulateBuildDefinitionList(string project = "SDK_v4")
        {
            // Documentation: https://docs.microsoft.com/en-us/rest/api/azure/devops/build/definitions/list?view=azure-devops-rest-5.0

            try
            {
                string restUrl = string.Format("https://{0}/{1}/{2}/_apis/build/definitions?includeAllProperties=true&api-version=5.0", buildDomain, organization, project);

                using (HttpClient client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Accept.Add(
                        new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
                        Convert.ToBase64String(
                            System.Text.ASCIIEncoding.ASCII.GetBytes(
                                string.Format("{0}:{1}", "", azurePersonalAccessToken))));

                    using (HttpResponseMessage response = await client.GetAsync(restUrl))
                    {
                        response.EnsureSuccessStatusCode();
                        string responseBody = await response.Content.ReadAsStringAsync();
                        JObject json = JObject.Parse(responseBody);

                        foreach (JObject o in json.Last.First)
                        {
                            BuildDef bd = new BuildDef();
                            bd.id = int.Parse(o["id"].ToString());
                            bd.name = o["name"].ToString();
                            bd.path = o["path"].ToString();
                            definitionList.Add(bd);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            definitionList.Sort(CompareBuildDefs);
        }

        public static async Task GetReleaseDefinitions(string project = "SDK_v4")
        {
            // Documentation: https://docs.microsoft.com/en-us/rest/api/azure/devops/release/definitions/list?view=azure-devops-rest-5.0

            try
            {
                string restUrl = string.Format("https://{0}/{1}/{2}/_apis/release/definitions?api-version=5.0", releaseDomain, organization, project);

                using (HttpClient client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Accept.Add(
                        new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
                        Convert.ToBase64String(
                            System.Text.ASCIIEncoding.ASCII.GetBytes(
                                string.Format("{0}:{1}", "", azurePersonalAccessToken))));

                    using (HttpResponseMessage response = await client.GetAsync(restUrl))
                    {
                        response.EnsureSuccessStatusCode();
                        string responseBody = await response.Content.ReadAsStringAsync();
                        JObject json = JObject.Parse(responseBody);

                        foreach (JObject o in json.Last.First)
                        {
                            BuildDef bd = new BuildDef();
                            bd.id = int.Parse(o["id"].ToString());
                            bd.name = o["name"].ToString();
                            bd.path = o["path"].ToString();
                            definitionList.Add(bd);
                        }
                    }
                }
            }
            catch (System.Net.Http.HttpRequestException)
            {
                // No releases found: 404
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            definitionList.Sort(CompareBuildDefs);
        }

        public static async Task WriteDefinitionToFile(string project, string definitionType, BuildDef buildDef)
        {
            // Documentation: https://docs.microsoft.com/en-us/rest/api/azure/devops/build/definitions/get?view=azure-devops-rest-5.0

            try
            {
                string restUrl = string.Format("https://{0}/{1}/{2}/_apis/{3}/definitions/{4}?api-version=5.0",
                    domain, organization, project, definitionType, buildDef.id);

                using (HttpClient client = new HttpClient())
                {
                    string definitionName = (buildDef.path + "\\" + buildDef.name).Replace("\\\\", "\\").TrimStart('\\');
                    Console.WriteLine("    * " + definitionName);
                    BuildCsvString(3, definitionName);

                    client.DefaultRequestHeaders.Accept.Add(
                        new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
                        Convert.ToBase64String(
                            System.Text.ASCIIEncoding.ASCII.GetBytes(
                                string.Format("{0}:{1}", "", azurePersonalAccessToken))));

                    using (HttpResponseMessage response = await client.GetAsync(restUrl))
                    {
                        response.EnsureSuccessStatusCode();
                        string responseBody = await response.Content.ReadAsStringAsync();
                        JObject json = JObject.Parse(responseBody);

                        isYamlPipeline = CheckForYamlPipeline(json);

                        // Write the json for the definition to a file
                        string fileContent = json.ToString();

                        string directory = outputPath + project + "\\" + definitionType + "s\\" + buildDef.path;
                        System.IO.Directory.CreateDirectory(directory);
                        string buildName = buildDef.name.Replace("?", "").Replace(":", "");
                        if (isYamlPipeline)
                        {
                            directory += "\\" + buildName;
                            System.IO.Directory.CreateDirectory(directory);
                        }
                        directory = directory.Replace("\\\\", "\\").Replace("\\\\", "\\");

                        string filePathWithoutExtension = directory + "\\" + buildDef.name.Replace("?", "").Replace(":", "").Replace(@"\", "-").Replace("/", "-");
                        // Limit file path to 259 characters to avoid System.IO.PathTooLongException.
                        int maxLength = Math.Min(filePathWithoutExtension.Length, 254);
                        string fullFilePath = filePathWithoutExtension.Substring(0, maxLength) + ".json";

                        System.IO.File.WriteAllText(fullFilePath, fileContent);
                        if (isYamlPipeline)
                        {
                            string saveDirectory = Directory.GetCurrentDirectory();
                            DownloadYamlFilesToDirectory(json, directory);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Write to file: " + ex.GetType());
                //throw;
            }
        }

        public static bool CheckForYamlPipeline(JObject json)
        {
            yamlFileNames.Clear();
            yamlFileNameDuplicates.Clear();

            if (json != null && json["process"] != null && json["process"]["yamlFilename"] != null)
            {
                yamlFileNames.Add(json["process"]["yamlFilename"].ToString());
                return true;
            }

            return false;
        }

        /// <summary>
        /// Download the root .yml file, that is, the file referenced in the definition json.
        /// Then recursively find all template references and download those .yml template files.
        /// </summary>
        /// <param name="json"></param>
        /// <param name="directory"></param>
        public static void DownloadYamlFilesToDirectory(JObject json, string directory)
        {
            if (json != null && json["repository"] != null && json["repository"]["properties"] != null)
            {
                // Get the git repo URL for this Azure project.
                string gitRepoBaseUrl = null;
                if (json["repository"]["properties"]["manageUrl"] != null)
                {
                    // Base URL for files in the github.com repo.
                    gitRepoBaseUrl = GetGithubRepoUrlMinusPath(json);
                }
                else if (json["repository"]["url"] != null)
                {
                    // Base URL for files in the Azure git repo, dev.azure.com. 
                    gitRepoBaseUrl = GetVsoRepoUrlMinusPath(json);
                }
                else if (json["repository"]["properties"]["cloneUrl"] != null)
                {
                    throw new Exception("This should never be called.");
                }

                if (!string.IsNullOrWhiteSpace(gitRepoBaseUrl))
                {
                    //  https://docs.microsoft.com/en-us/rest/api/azure/devops/build/definitions/get?view=azure-devops-rest-5.1#buildrepository

                    string githubFileUrl = string.Empty;
                    string logIndent = string.Empty;
                    string currentOffsetFromBaseUrl = string.Empty;

                    if (yamlFileNames.Count > 0)
                    {
                        // Set githubFileUrl for the first yaml file. (That's the one named in the .json build definition.)
                        string firstFile = yamlFileNames.FirstOrDefault();
                        githubFileUrl = gitRepoBaseUrl + "/" + firstFile;
                        logIndent = "      "; // 6 spaces
                    }

                    while (yamlFileNames.Count > 0)
                    {
                        // Download .yml file from repo.
                        // Look for template references to other .yml files. Add them to yamlFileNames list.
                        // Loop.
                        string filePath = yamlFileNames.FirstOrDefault();

                        currentOffsetFromBaseUrl = filePath.Contains('/') ? filePath.Substring(0, filePath.LastIndexOf('/')) : "";

                        if (string.IsNullOrWhiteSpace(githubFileUrl))
                        {
                            // Set githubFileUrl for the next yaml file.
                            githubFileUrl = gitRepoBaseUrl + filePath;
                            logIndent = "        "; // 8 spaces
                        }

                        // Check whether this file has the same name as another. If so, it wil need its own directory to avoid a file overwrite.
                        string outputFilePath;
                        string fileName;
                        string duplicateFileName = yamlFileNameDuplicates.FirstOrDefault(x => filePath.EndsWith(x, StringComparison.OrdinalIgnoreCase));
                        if (!string.IsNullOrWhiteSpace(duplicateFileName))
                        {
                            // It does. Create a folder for it.
                            fileName = duplicateFileName.Replace('/', '\\');
                            string newDir = $"{directory}\\{fileName.Substring(0, fileName.LastIndexOf('\\'))}";
                            System.IO.Directory.CreateDirectory(newDir);
                        }
                        else
                        {
                            fileName = githubFileUrl.Substring(githubFileUrl.LastIndexOf('/') + 1);
                        }
                        outputFilePath = $"{directory}\\{fileName}";

                        bool succeeded = DownloadFileFromGithub(githubFileUrl, outputFilePath, fileName, logIndent);
                        yamlFileNames.RemoveAt(0);

                        if (succeeded)
                        {
                            int count = yamlFileNames.Count;
                            AddYamlTemplateReferencesFromFile(outputFilePath, currentOffsetFromBaseUrl, ref yamlFileNames);
                        }
                        githubFileUrl = string.Empty;
                    }
                }
                else
                {
                    Console.WriteLine("Could not get gitRepoBaseUrl value for .yml file.");
                }
            }
        }

        private static string GetVsoRepoUrlMinusPath(JObject json)
        {
            string repoUrl = "";
            string jsonUrl = json["repository"]["url"].ToString();
            string[] jsonUrlSplit = jsonUrl.Split('/');

            if (jsonUrlSplit.Length >= 7)
            {
                string organization = jsonUrlSplit[3];
                string project = jsonUrlSplit[4];
                string repository = jsonUrlSplit[6];
                string commitOrBranch = json["repository"]["defaultBranch"].ToString();

                repoUrl = $"https://dev.azure.com/{organization}/{project}/_apis/sourceProviders/tfsgit" +
                    $"/filecontents?&repository={repository}&commitOrBranch={commitOrBranch}&api-version=5.0-preview.1&path=";
            }
            else
            {
                // Not enough split elements to form a valid repo URL. Repo may no longer be available.
                Console.WriteLine($"\nRepo not available: {jsonUrl}");
                repoUrl = jsonUrl;
            }

            return repoUrl;
        }

        private static string GetGithubRepoUrlMinusPath(JObject json)
        {
            string jsonUrl = json["repository"]["url"].ToString();
            string organization = jsonUrl.Split('/')[3];
            string project = jsonUrl.Split('/')[4].Split('.')[0];
            string commitOrBranch = json["repository"]["defaultBranch"].ToString();
            string repoUrl = $"https://raw.githubusercontent.com/{organization}/{project}/{commitOrBranch}/";

            return repoUrl;
        }

        private static bool DownloadFileFromGithub(string githubFileUrl, string outputFilePath, string fileName, string logIndent)
        {
            // https://gist.github.com/EvanSnapp/ddf7f7f793474ea9631cbc0960295983
            // https://github.com/zayenCh/DownloadFile/blob/master/Downloader.cs

            int column = (logIndent.Length / 2) + 1;
            if (!string.IsNullOrWhiteSpace(githubPersonalAccessToken) && githubFileUrl.Contains("githubusercontent"))
            {
                githubFileUrl += $"?access_token={githubPersonalAccessToken}";
            }

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("user-agent", "Anything");

                if (!githubFileUrl.Contains("githubusercontent"))
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
                            Convert.ToBase64String(
                                Encoding.ASCII.GetBytes(
                                    string.Format("{0}:{1}", "", azurePersonalAccessToken))));

                try
                {
                    var response = client.GetAsync(githubFileUrl).Result;

                    //Write content to file regardless of success
                    string contents = response.Content.ReadAsStringAsync().Result;
                    File.WriteAllText(outputFilePath, contents);

                    if (response.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"{logIndent}* {fileName}");
                        BuildCsvString(column, fileName);

                        return true;
                    }
                    else
                    {
                        Console.WriteLine($"{logIndent}* {fileName}**");
                        BuildCsvString(column, fileName + "**");

                        Console.WriteLine($"* {response.StatusCode} ({(int)response.StatusCode}) {githubFileUrl}");

                        return false;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"{logIndent}* {fileName}**");
                    BuildCsvString(column, fileName + "**");
                    // We couldn't get the file contents, so write the error message to the file instead.
                    string contents = $"* {ex.Message}\r\n{githubFileUrl}";
                    File.WriteAllText(outputFilePath, contents);
                    if (ex.Message.ToString().Contains("(404)"))
                    {
                        //Console.WriteLine($"Not Found (404): {githubFileUrl}");
                    }
                    else
                    {
                        Console.WriteLine($"* {ex.Message} {githubFileUrl}");
                    }
                }
            }
            return false;
        }

        private static void AddYamlTemplateReferencesFromFile(
            string targetFullFilePath, string currentOffsetFromRootUrl, ref List<string> yamlFileNames)
        {
            string match = "template:";

            var lines = File.ReadLines(targetFullFilePath);
            foreach (string line in lines)
            {
                if (line.Contains(match))
                {
                    string name = line.Trim();
                    if (name.StartsWith("::")) name = ""; // Delete an inline script comment.
                    int index = name.IndexOf("#"); // Find and remove any other comment.
                    if (index >= 0) name = name.Remove(index).TrimEnd();
                    name = name.TrimStart('-').Replace(match, "").Trim(); // TrimStart() handles the case of "- template:".
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        // Normalize the relative path.
                        name = Path.GetFullPath((Path.Combine("/", currentOffsetFromRootUrl, name))).Replace(@"C:\", "").TrimStart('\\').Replace(@"\", "/");
                        if (!yamlFileNames.Contains(name))
                        {
                            AddToYamlFileNameDuplicates(name);
                            yamlFileNames.Add(name);
                        }
                    }
                }
            }

            return;
        }

        /// <summary>
        /// This checks filePath to see whether the file name duplicates a file name already in 
        /// yamlFileNames. If so, it adds the two files to yamlFileNameDuplicates. 
        /// For files in yamlFileNameDuplicates we make an exception to folder flattening and 
        /// create separate download folders to prevent one file overwriting the other.
        /// </summary>
        /// <param name="filePath"></param>
        private static void AddToYamlFileNameDuplicates(string filePath)
        {
            if (yamlFileNames.Contains(filePath))
            {
                // This is a duplicate reference to the same file. We don't dedupe these. Skip it.
                return;
            }

            string[] filePathArray = filePath.Split('/');
            filePathArray = filePathArray.Reverse().ToArray();

            foreach (string yamlFileName in yamlFileNames)
            {
                string[] yamlFileNameArray = yamlFileName.Split('/');
                yamlFileNameArray = yamlFileNameArray.Reverse().ToArray();

                for (int i = 0; i < filePathArray.Length && i < yamlFileNameArray.Length; i++)
                {
                    if (!filePathArray[i].Equals(yamlFileNameArray[i], StringComparison.OrdinalIgnoreCase))
                    {
                        if (i == 0) break; // Names are not dupes.

                        string outFilePath = "";
                        string outyamlPath = "";
                        int j = i;
                        while (j >= 0)
                        {
                            outFilePath += filePathArray[j] + "/";
                            outyamlPath += yamlFileNameArray[j] + "/";
                            j--;
                        }

                        yamlFileNameDuplicates.Add(outFilePath.Trim('/'));
                        yamlFileNameDuplicates.Add(outyamlPath.Trim('/'));
                        return; // No more dupes that are not accounted for, so quit.
                    }
                }
            }
        }

        private static int CompareProjects(Proj x, Proj y)
        {
            if (x == null)
            {
                if (y == null)
                {
                    return 0;
                }
                else
                {
                    return -1;
                }
            }
            else
            {
                if (y == null)
                {
                    return 1;
                }
                else
                {
                    return x.name.ToLowerInvariant().CompareTo(y.name.ToLowerInvariant());
                }
            }
        }

        private static int CompareBuildDefs(BuildDef x, BuildDef y)
        {
            if (x == null)
            {
                if (y == null)
                {
                    return 0;
                }
                else
                {
                    return -1;
                }
            }
            else
            {
                if (y == null)
                {
                    return 1;
                }
                else
                {
                    return (x.path + @"\" + x.name).ToLowerInvariant().CompareTo((y.path + @"\" + y.name).ToLowerInvariant());
                }
            }
        }

        static string csvString = string.Empty;
        static List<string> csvColumns = new List<string>();
        static int lastColumnNumber = -1;

        static void BuildCsvString(int columnNumber, string columnValue)
        {
            // Append to csvString.
            if (columnNumber <= lastColumnNumber)
            {
                string newRow = string.Empty;

                foreach (string column in csvColumns)
                {
                    newRow += "=\"" + column + "\",";
                }

                csvString += newRow.Trim(',') + "\r\n";
            }

            // Add column(s) only if columnValue has a value.
            if (!string.IsNullOrWhiteSpace(columnValue))
            {
                while (csvColumns.Count >= columnNumber)
                {
                    csvColumns.RemoveAt(csvColumns.Count - 1);
                }

                foreach (string column in columnValue.Split(','))
                {
                    csvColumns.Add(column);
                }
            }

            lastColumnNumber = columnNumber;
        }
    }
}
