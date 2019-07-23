using Newtonsoft.Json.Linq;
//using Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace ExportBuildDefinitions
{
    /// <summary>
    /// Exports all build and release definitions in the organization to .json files.
    /// </summary>
    /// <remarks>
    /// Unfortunately the build definition output is not importable via the Azure DevOps UI to create a new definition.
    /// Comparing release definitions, however, they are nearly identical to the ones exported by the UI. Only difference being 
    /// some enumerables are represented by integers instead of names. Have not tried importing those.
    /// </remarks>
    class Program
    {
        // Set up the following three vars in file Settings.settings to use this program.
        //
        // personalAccessToken description: https://docs.microsoft.com/en-us/azure/devops/integrate/get-started/authentication/pats?view=azure-devops
        static string personalAccessToken = Properties.Settings.Default.personalAccessToken;
        // organization is the value in your Azure DevOps URL: https://dev.azure.com/{yourorganization}
        static string organization = Properties.Settings.Default.organization;
        // outputPath defines where the .json files will be written. Requires a trailing backslash. Example: @"C:\temp\BuildDefinitions\"
        static string outputPath = Properties.Settings.Default.outputPath;

        const string buildDomain = "dev.azure.com";
        const string releaseDomain = "vsrm.dev.azure.com";
        static string domain = buildDomain;

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
            //GetBuilds();
            GetProjects().Wait();
            Console.WriteLine("Writing .json files to: " + outputPath + "\n");

            foreach (Proj proj in projectList)
            {
                Console.WriteLine(proj.name + "------------");

                definitionList.Clear();
                domain = buildDomain;
                GetBuildDefinitions(proj.name).Wait();
                if (definitionList.Count > 0) { Console.WriteLine("  builds");  }
                foreach (BuildDef buildDef in definitionList)
                {
                    WriteDefinitionToFile(proj.name, "build", buildDef).Wait();
                }

                definitionList.Clear();
                domain = releaseDomain;
                GetReleaseDefinitions(proj.name).Wait();
                if (definitionList.Count > 0) { Console.WriteLine("  releases"); }
                foreach (BuildDef buildDef in definitionList)
                {
                    WriteDefinitionToFile(proj.name, "release", buildDef).Wait();
                }
            }
            Console.WriteLine("Press any key");
            Console.ReadKey();
        }

        public static async Task GetProjects()
        {
            // Documentation: https://docs.microsoft.com/en-us/rest/api/azure/devops/?view=azure-devops-server-rest-5.0
            //    https://docs.microsoft.com/en-us/rest/api/azure/devops/core/projects/list?view=azure-devops-rest-5.1

            try
            {
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
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        public static async Task GetBuildDefinitions(string project = "SDK_v4")
        {
            // Documentation: https://docs.microsoft.com/en-us/rest/api/azure/devops/build/definitions/list?view=azure-devops-rest-5.0

            try
            {
                string restUrl = String.Format("https://{0}/{1}/{2}/_apis/build/definitions?api-version=5.0", buildDomain, organization, project);

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
                    Console.WriteLine("    " + (buildDef.path + "\\" + buildDef.name).Replace("\\\\","\\").TrimStart('\\'));

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
                        // Write json to a file
                        string fileContent = json.ToString();

                        string directory = outputPath + project + "\\" + definitionType + "s\\" + buildDef.path;
                        System.IO.Directory.CreateDirectory(directory);
                        string fullFilePath = directory + "\\" + buildDef.name.Replace("?", "").Replace(":", "") + ".json";
                        System.IO.File.WriteAllText(fullFilePath, fileContent);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

    }
}
