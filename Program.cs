using Ionic.Zip;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Diagnostics;

namespace BP_ClientPatch_Updater {
    internal class Program {
        const string fileName_MostRecentPatch = "most-recent-patch.json";
        static string urlPath_MostRecentPatch = "https://raw.githubusercontent.com/DOTzX/BP-ClientPatch-Updater/main/most-recent-patch.json?t=" + Convert.ToString((int) DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds);

        const string fileName_bptlSetting = "bpcpu_setting.json";
        static string filePath_bptlSetting = Path.Combine(Directory.GetCurrentDirectory(), fileName_bptlSetting);

        const string fileName_zip = "modpak.zip";
        static string filePath_zip = Path.Combine(Directory.GetCurrentDirectory(), fileName_zip);

        const string locVersionFormat = "YYYYMMDD-HHmm";
        static string applicationVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();

        public static async Task<int> Main(string[] args) {
            Console.Title = "app";

            bool isDownload = !File.Exists(filePath_zip);
            bool isSave = false;
            bool isUnpack = false;

            JObject jsonObjectAppSetting = new JObject();
            string selectedLanguage = "en";
            double installedVersion = 0.0;

            JObject jsonOjectMostRecentPatch = new JObject();
            string appLatestVersion = "0.0.0.0";
            double latestVersion = 0.0;
            string alternativeLanguageName = "";
            string urlPath_LocZip = "";
            string joinedAvailable = "";

            Console.Write($"[INIT] Loading file '{fileName_bptlSetting}'...");
            if (File.Exists(filePath_bptlSetting)) {
                string jsonString = File.ReadAllText(filePath_bptlSetting);
                try {
                    jsonObjectAppSetting = JObject.Parse(jsonString);

                    try {
                        selectedLanguage = (string) jsonObjectAppSetting["selected_language"];
                    } catch (Exception) {
                        jsonObjectAppSetting["selected_language"] = selectedLanguage;
                        isSave = true;
                    }

                    try {
                        installedVersion = (double) jsonObjectAppSetting[$"installed_version_{selectedLanguage}"];
                    } catch (Exception) { }

                    Console.WriteLine($"\r[INIT] Success to load '{fileName_bptlSetting}'");
                } catch (Exception e) {
                    Console.WriteLine($"\r[INIT] Fail to load '{fileName_bptlSetting}', skipping.\n{e.Message}");
                }
            } else {
                Console.WriteLine($"\r[INIT] File not found: '{fileName_bptlSetting}', skipping.");
                jsonObjectAppSetting["selected_language"] = selectedLanguage;
                jsonObjectAppSetting[$"installed_version_{selectedLanguage}"] = installedVersion;
                isSave = true;
            }

            Console.Write($"[INIT] Loading '{fileName_MostRecentPatch}' from remote url...");
            try {
                using (HttpClient client = new HttpClient()) {
                    client.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue {
                        NoCache = true
                    };
                    HttpResponseMessage response = await client.GetAsync(urlPath_MostRecentPatch);
                    if (response.IsSuccessStatusCode) {
                        string jsonString = await response.Content.ReadAsStringAsync();
                        jsonOjectMostRecentPatch = JObject.Parse(jsonString);
                        Console.WriteLine($"\r[INIT] Success to load '{fileName_MostRecentPatch}' from remote url.");
                    } else {
                        Console.WriteLine($"\r[INIT] Fail to load '{fileName_MostRecentPatch}' from remote url, skipping. Status Code: {response.StatusCode}");
                    }
                }
            } catch (Exception e) {
                Console.WriteLine($"\r[INIT] Fail to load '{fileName_MostRecentPatch}' from remote url, skipping.\n{e.Message}");
            }

            if (jsonOjectMostRecentPatch.Properties().Any()) {
                appLatestVersion = (string) jsonOjectMostRecentPatch["_appver"];
                JArray availableLanguage = jsonOjectMostRecentPatch["_available"] as JArray;
                joinedAvailable = string.Join(", ", availableLanguage.Select(item => (string) item));
                if (!availableLanguage.Any(item => (string) item == selectedLanguage)) {
                    Console.WriteLine($"[WARN] Invalid selected language: '{selectedLanguage}', revert to '{availableLanguage[0]}'");
                    selectedLanguage = (string) availableLanguage[0];
                    jsonObjectAppSetting["selected_language"] = selectedLanguage;
                    isSave = true;
                }

                if (jsonOjectMostRecentPatch.ContainsKey(selectedLanguage)) {
                    JArray arraySelectedLanguage = jsonOjectMostRecentPatch[selectedLanguage] as JArray;
                    latestVersion = (double) arraySelectedLanguage[0];
                    alternativeLanguageName = $" ({arraySelectedLanguage[1]})";
                    urlPath_LocZip = (string) arraySelectedLanguage[2];
                }
            }

            Console.WriteLine($"[APP] Installed Version: {applicationVersion}");

            if (appLatestVersion != "0.0.0.0" && appLatestVersion != applicationVersion) {
                Console.WriteLine($"[APP] New Version Found: {appLatestVersion}, download: https://github.com/DOTzX/BP-ClientPatch-Updater/releases");
            } else {
                Console.WriteLine($"[APP] Latest Version: {appLatestVersion}");
            }

            if (joinedAvailable.Length > 0) Console.WriteLine($"[LOC] Available language: {joinedAvailable}");
            Console.WriteLine($"[LOC] Selected Language: {selectedLanguage}{alternativeLanguageName}");
            Console.WriteLine($"[LOC] Installed Version: {installedVersion.ToString().Replace(',', '-').Replace('.', '-').PadRight(locVersionFormat.Length, '0')}");

            if (latestVersion != 0.0 && latestVersion > installedVersion) {
                isDownload = true;
                Console.WriteLine($"[LOC] New Version Found: {latestVersion.ToString().Replace(',', '-').Replace('.', '-').PadRight(locVersionFormat.Length, '0')}");
            } else {
                Console.WriteLine($"[LOC] Latest Version: {latestVersion.ToString().Replace(',', '-').Replace('.', '-').PadRight(locVersionFormat.Length, '0')}");
            }

            if (isDownload) {
                Console.Write($"[INIT] Downloading '{fileName_zip}' from remote url...");
                try {
                    using (HttpClient client = new HttpClient()) {
                        HttpResponseMessage response = await client.GetAsync(urlPath_LocZip);

                        if (response.IsSuccessStatusCode) {
                            using (FileStream fileStream = File.Create(filePath_zip)) {
                                await response.Content.CopyToAsync(fileStream);
                            }
                            isUnpack = true;
                            ClearCurrentConsoleLine();
                            Console.WriteLine($"\r[INIT] Downloaded '{fileName_zip}'");
                        } else {
                            Console.WriteLine($"\r[INIT] Fail to download '{fileName_zip}' from remote url, skipping. Status Code: {response.StatusCode}");
                        }
                    }
                } catch (Exception e) {
                    Console.WriteLine($"\r[INIT] Fail to download '{fileName_zip}' from remote url, skipping.\n{e.Message}");
                }
            }

            if (isUnpack) {
                Console.Write($"[INIT] Unpacking '{fileName_zip}'...");
                try {
                    string extractDirectory = Directory.GetCurrentDirectory();

                    using (ZipFile zip = ZipFile.Read(filePath_zip)) {
                        zip.ExtractAll(extractDirectory, ExtractExistingFileAction.OverwriteSilently);
                    }

                    jsonObjectAppSetting[$"installed_version_{selectedLanguage}"] = latestVersion;
                    isSave = true;

                    ClearCurrentConsoleLine();
                    Console.WriteLine($"\r[INIT] Unpacked '{fileName_zip}'");
                    Console.WriteLine($"\n\n[INFO] Please replace existing the .PAK file on the current folder into inside ~mods folder.\n\n");

                    string currentDirectory = System.IO.Directory.GetCurrentDirectory();
                    Process.Start("explorer.exe", currentDirectory);
                } catch (Exception e) {
                    ClearCurrentConsoleLine();
                    Console.WriteLine($"\r[INIT] Fail to unpack '{fileName_zip}', skipping.\n{e.Message}");
                }
            }

            if (isSave) {
                Console.Write($"[INIT] Saving '{fileName_bptlSetting}'...");
                File.WriteAllText(filePath_bptlSetting, jsonObjectAppSetting.ToString());
                ClearCurrentConsoleLine();
                Console.WriteLine($"\r[INIT] Saved '{fileName_bptlSetting}'");
            }

            Console.WriteLine($"\n\n[INFO] You can close this app.");

            while (true) Console.ReadKey();
        }

        static void ClearCurrentConsoleLine() {
            int currentLineCursor = Console.CursorTop;
            Console.SetCursorPosition(0, Console.CursorTop);
            Console.Write(new string(' ', Console.WindowWidth));
            Console.SetCursorPosition(0, currentLineCursor);
        }


    }
}
