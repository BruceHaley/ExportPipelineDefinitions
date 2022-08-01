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
using System.Runtime.InteropServices;

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
        // Before running this program, set up the following three vars in file Properties\Settings.settings.
        //
        // personalAccessToken description: https://docs.microsoft.com/en-us/azure/devops/integrate/get-started/authentication/pats?view=azure-devops
        // organization is the value in your Azure DevOps URL: https://dev.azure.com/{yourorganization}
        // outputPath defines where the .json files will be written. Requires a trailing backslash. Example: @"C:\temp\PipelineDefinitions\"

        static string personalAccessToken;
        static string organization;
        static string outputPath;

        const string buildDomain = "dev.azure.com";
        const string releaseDomain = "vsrm.dev.azure.com";
        static string domain = buildDomain;
        static bool isYamlPipeline = false;
        static List<string> yamlFileNames = new List<string>();

        public class BuildDef
        {
            public int id;
            public string name;
            public string path;
            public string type;
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
                        Console.WriteLine("  * builds");
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
                        Console.WriteLine("  * releases");
                        BuildCsvString(2, "releases");
                    }
                    foreach (BuildDef buildDef in definitionList)
                    {
                        WriteDefinitionToFile(proj.name, "release", buildDef).Wait();
                    }
                }
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
                File.WriteAllText(outputPath + Path.DirectorySeparatorChar + @"BuildDefinitions.csv", csvString);
            }

            Console.WriteLine("Done.");
        }

        public static void GetSettings()
        {
            personalAccessToken = Environment.GetEnvironmentVariable("personalAccessToken");
            organization = Environment.GetEnvironmentVariable("organization");
            outputPath = Environment.GetEnvironmentVariable("outputPath");

            Validate(nameof(personalAccessToken), personalAccessToken);
            Validate(nameof(organization), organization);
            Validate(nameof(outputPath), outputPath);
        }

        public static void Validate(string nameOfVar, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidDataException(
                    string.Format(
                        "Invalid {0} value in environment variable",
                        nameOfVar,
                        System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName));
            }
        }

        public static async Task PopulateProjectList()
        {
            // Documentation: https://docs.microsoft.com/en-us/rest/api/azure/devops/?view=azure-devops-server-rest-5.0
            //    https://docs.microsoft.com/en-us/rest/api/azure/devops/core/projects/list?view=azure-devops-rest-5.1

            string restUrl = String.Format("https://{0}/{1}/_apis/projects", domain, organization);
            Console.WriteLine("Getting build definitions from " + restUrl);

            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Add(
                    new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
                    Convert.ToBase64String(
                        System.Text.ASCIIEncoding.ASCII.GetBytes(
                            string.Format("{0}:{1}", "", personalAccessToken))));

                using (HttpResponseMessage response = await client.GetAsync(restUrl))
                {
                    response.EnsureSuccessStatusCode();
                    string responseBody = await response.Content.ReadAsStringAsync();
                    if (responseBody.Contains("!DOCTYPE"))
                    {
                        throw new AccessViolationException(
                            string.Format(
                                "Cannot access Azure. Please ensure personalAccessToken and organization values are correct in file {0}.config.",
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
            projectList.Sort(CompareProjs);
        }

        public static async Task PopulateBuildDefinitionList(string project = "SDK_v4")
        {
            // Documentation: https://docs.microsoft.com/en-us/rest/api/azure/devops/build/definitions/list?view=azure-devops-rest-5.0

            try
            {
                //string restUrl = String.Format("https://{0}/{1}/{2}/_apis/build/definitions?api-version=5.0", buildDomain, organization, project);
                string restUrl = String.Format("https://{0}/{1}/{2}/_apis/build/definitions?includeAllProperties=true&api-version=5.0", buildDomain, organization, project);

                using (HttpClient client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Accept.Add(
                        new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
                        Convert.ToBase64String(
                            System.Text.ASCIIEncoding.ASCII.GetBytes(
                                string.Format("{0}:{1}", "", personalAccessToken))));

                    using (HttpResponseMessage response = await client.GetAsync(restUrl))
                    {
                        response.EnsureSuccessStatusCode();
                        string responseBody = await response.Content.ReadAsStringAsync();
                        JObject json = JObject.Parse(responseBody);
                        //Object json = JsonParser.Deserialize(responseBody);
                        foreach (JObject o in json.Last.First)
                        {
                            BuildDef bd = new BuildDef();
                            bd.id = int.Parse(o["id"].ToString());
                            bd.name = o["name"].ToString();
                            bd.path = o["path"].ToString();
                            bd.type = o.SelectToken("repository.type").ToString();
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
                string restUrl = String.Format("https://{0}/{1}/{2}/_apis/release/definitions?api-version=5.0", releaseDomain, organization, project);

                using (HttpClient client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Accept.Add(
                        new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
                        Convert.ToBase64String(
                            System.Text.ASCIIEncoding.ASCII.GetBytes(
                                string.Format("{0}:{1}", "", personalAccessToken))));

                    using (HttpResponseMessage response = await client.GetAsync(restUrl))
                    {
                        response.EnsureSuccessStatusCode();
                        string responseBody = await response.Content.ReadAsStringAsync();
                        JObject json = JObject.Parse(responseBody);
                        //Object json = JsonParser.Deserialize(responseBody);
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

        public static async Task WriteDefinitionToFile(string project, string definitionType, BuildDef buildDef)
        {
            // Documentation: https://docs.microsoft.com/en-us/rest/api/azure/devops/build/definitions/get?view=azure-devops-rest-5.0

            try
            {
                string restUrl = String.Format("https://{0}/{1}/{2}/_apis/{3}/definitions/{4}?api-version=5.0", 
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
                                string.Format("{0}:{1}", "", personalAccessToken))));

                    using (HttpResponseMessage response = await client.GetAsync(restUrl))
                    {
                        response.EnsureSuccessStatusCode();
                        string responseBody = await response.Content.ReadAsStringAsync();
                        JObject json = JObject.Parse(responseBody);

                        isYamlPipeline = CheckForYamlPipeline(json);

                        // Write json to a file
                        string fileContent = json.ToString();

                        string directory = outputPath + project + Path.DirectorySeparatorChar + definitionType + "s" + Path.DirectorySeparatorChar + buildDef.path;
                        directory = directory.Replace("/\\", "/");
                        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                        {
                            directory = directory.Replace("\\", "/");
                        }
                        System.IO.Directory.CreateDirectory(directory);
                        string buildName = buildDef.name.Replace("?", "").Replace(":", "");
                        if (isYamlPipeline)
                        {
                            directory += Path.DirectorySeparatorChar + buildName;
                            System.IO.Directory.CreateDirectory(directory);
                        }

                        string fullFilePath = directory + Path.DirectorySeparatorChar + buildDef.name.Replace("?", "").Replace(":", "") + ".json";
                        System.IO.File.WriteAllText(fullFilePath, fileContent);
                        if (isYamlPipeline)
                        {
                            string saveDirectory = Directory.GetCurrentDirectory();
                            //Directory.SetCurrentDirectory(directory);
                            DownloadYamlFilesToDirectory(json, directory, project, buildDef.type);
                            //Directory.SetCurrentDirectory(saveDirectory);
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

            if (json != null && json["process"] != null && json["process"]["yamlFilename"] != null)
            {
                yamlFileNames.Add(json["process"]["yamlFilename"].ToString());
                return true;
            }

            return false;
        }

        public static void DownloadYamlFilesToDirectory(JObject json, string directory, string project, string type)
        {
            // Download the .yml files from the GitHub repo.
            // Get GitHub repo URL for this Azure project.
            if (json != null && json["repository"] != null && json["repository"]["properties"] != null)
            {
                string gitRepoUrl = null;
                if (json["repository"]["properties"]["manageUrl"] != null)
                {
                    // This is for the github.com repo. It works.
                    gitRepoUrl = json["repository"]["properties"]["manageUrl"].ToString();
                }
                else if (json["repository"]["properties"]["cloneUrl"] != null)
                {
                    // This is for the visualstudio.com git repo. It does not work.
                    // TODO: Make the necessary API call to get this yaml file. 
                    gitRepoUrl = json["repository"]["properties"]["cloneUrl"].ToString();
                }
                else if (json["repository"]["url"] != null)
                {
                    // This gets dev.azure.com. It does not work.
                    // TODO: Make the necessary API call to get this yaml file. 
                    gitRepoUrl = json["repository"]["url"].ToString();
                }

                if (!string.IsNullOrWhiteSpace(gitRepoUrl))
                {
                    string defaultBranch = string.Empty;

                    if (json["repository"] != null && json["repository"]["defaultBranch"] != null)
                    {
                        defaultBranch = json["repository"]["defaultBranch"].ToString();
                    }
                    gitRepoUrl += string.IsNullOrWhiteSpace(defaultBranch) ? "" : $"/raw/{defaultBranch}";

                    if (!string.IsNullOrEmpty(gitRepoUrl))
                    {
                        //  https://docs.microsoft.com/en-us/rest/api/azure/devops/build/definitions/get?view=azure-devops-rest-5.1#buildrepository
                        //  BuildRepository Represents a repository used by a build definition.
                        // Begin loop.
                        // Get next .yml file name from yamlFileNames list.
                        // Set yamlFilesRootUrl and githubFileUrl for the first yaml file (The one named in the .json build definition.)
                        string yamlFilesRootUrl = gitRepoUrl;
                        string githubFileUrl = string.Empty;
                        string logIndent = string.Empty;

                        if (yamlFileNames.Count > 0)
                        {
                            string firstFile = yamlFileNames.FirstOrDefault();
                            yamlFilesRootUrl += firstFile.Contains('/') ? "/" + firstFile.Substring(0, firstFile.LastIndexOf('/')) : "";
                            githubFileUrl = gitRepoUrl + "/" + firstFile;
                            logIndent = "      "; // 6 spaces
                        }
                        
                        while (yamlFileNames.Count > 0)
                        {
                            string filePath = yamlFileNames.FirstOrDefault();

                            if (string.IsNullOrWhiteSpace(githubFileUrl))
                            {
                                githubFileUrl = yamlFilesRootUrl + "/" + filePath;
                                logIndent = "        "; // 8 spaces
                            }
                            string fileName = githubFileUrl.Substring(githubFileUrl.LastIndexOf('/') + 1);
                            string targetFullFilePath = directory + Path.DirectorySeparatorChar + fileName;

                            if (type == "TfsGit")
                            {
                                string restUrl = "";
                                try {
                                    restUrl = String.Format("https://{0}/{1}/{2}/_apis/pipelines/{3}/preview?api-version=7.1-preview.1", domain, organization, project, json["id"]);

                                    using (HttpClient client = new HttpClient())
                                    {
                                        client.DefaultRequestHeaders.Accept.Add(
                                            new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
                                            Convert.ToBase64String(
                                                System.Text.ASCIIEncoding.ASCII.GetBytes(
                                                    string.Format("{0}:{1}", "", personalAccessToken))));

                                        var requestContent = new StringContent("{\"previewRun\":true}", Encoding.UTF8, "application/json");
                                        var webRequest = new HttpRequestMessage(HttpMethod.Post, restUrl)
                                        {
                                            Content = requestContent
                                        };
                                        using (HttpResponseMessage response = client.Send(webRequest))
                                        {
                                            response.EnsureSuccessStatusCode();
                                            using var reader = new StreamReader(response.Content.ReadAsStream());
                                            string responseBody = reader.ReadToEnd();
                                            if (responseBody.Contains("!DOCTYPE"))
                                            {
                                                throw new AccessViolationException(
                                                    string.Format(
                                                        "Cannot access Azure. Please ensure personalAccessToken and organization values are correct in file {0}.config.",
                                                        System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName));
                                            }
                                            JObject jsonNew = JObject.Parse(responseBody);
                                            using (StreamWriter outputFile = new StreamWriter(targetFullFilePath))
                                            {
                                                outputFile.Write(jsonNew["finalYaml"].ToString());
                                            }
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine(ex.ToString());
                                    Console.WriteLine(restUrl);
                                }

                                // There can be only one Azure DevOps YAML file per pipeline so we can break here.
                                break;
                            }
                            else
                            {
                                bool succeeded = DownloadFileFromGithub(githubFileUrl, targetFullFilePath, logIndent);
                                yamlFileNames.RemoveAt(0);
                                if (succeeded)
                                {
                                    int count = yamlFileNames.Count;
                                    yamlFileNames.AddRange(GetYamlTemplateReferencesFromFile(targetFullFilePath));
                                }
                                githubFileUrl = string.Empty;
                            }
                        }
                        // Read .yml file from repo.
                        // Look for template references to other .yml files. Add any found to yamlFileNames list.
                        // Write .yml file to directory.
                        // Loop.
                    }
                    else
                    {
                        Console.WriteLine("Could not get gitRepoUrl value for .yml file.");
                    }
                }
            }

            //Directory.SetCurrentDirectory(savedDirectory);
        }

        private static bool DownloadFileFromGithub(string githubFileUrl, string fullFilePath, string logIndent)
        {
            // https://gist.github.com/EvanSnapp/ddf7f7f793474ea9631cbc0960295983
            // https://github.com/zayenCh/DownloadFile/blob/master/Downloader.cs

            int column = (logIndent.Length / 2) + 1;

            WebClient webClient = new WebClient();
            webClient.Headers.Add("user-agent", "Anything");
            try
            {
                //webClient.DownloadFile(new Uri(githubFileUrl), fullFilePath.TrimEnd('\\'));
                string contents = webClient.DownloadString(new Uri(githubFileUrl));
                File.WriteAllText(fullFilePath, contents);
                string fileName = fullFilePath.Substring(fullFilePath.LastIndexOf("\\") + 1);
                Console.WriteLine($"{logIndent}* {fileName}");
                BuildCsvString(column, fileName);

                return true;
            }
            catch (Exception ex)
            {
                string fileName = fullFilePath.Substring(fullFilePath.LastIndexOf("\\") + 1);
                Console.WriteLine($"{logIndent}* {fileName}*");
                BuildCsvString(column, fileName + "*");
                string contents = $"* {ex.Message.ToString()}\r\n{githubFileUrl}";
                File.WriteAllText(fullFilePath, contents);
                if (ex.Message.ToString().Contains("(404)"))
                {
                    //Console.WriteLine($"Not Found (404): {githubFileUrl}");
                }
                else
                {
                    Console.WriteLine($"* {ex.Message.ToString()} {githubFileUrl}");
                }
            }
            return false;
        }

        private static List<string> GetYamlTemplateReferencesFromFile(string targetFullFilePath)
        {
            List<string> references = new List<string>();
            string match = "- template:";

            var lines = File.ReadLines(targetFullFilePath);
            foreach (string line in lines)
            {
                if (line.Contains(match))
                {
                    string name = line.Replace(match, "").Trim();
                    if (!references.Contains(name))
                    {
                        references.Add(name);
                    }
                }
            }

            return references;
        }

        private static int CompareProjs(Proj x, Proj y)
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
