using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Mono.Cecil;
using SybarisLoader.Util;

namespace SybarisLoader
{
    /// <summary>
    ///     The entry point class of patch loader.
    /// </summary>
    /// <remarks>
    ///     At the moment this loader requires to System.dll being loaded into memroy to work, which is why it cannot be
    ///     patched with this method.
    /// </remarks>
    public static class Loader
    {
        private static Dictionary<string, List<MethodInfo>> patchersDictionary;

        public static void LoadPatchers()
        {
            patchersDictionary = new Dictionary<string, List<MethodInfo>>();

            Logger.Log(LogLevel.Info, "Loading patchers");

            foreach (string dll in Directory.GetFiles(Utils.SybarisDir, "*.Patcher.dll"))
            {
                Assembly assembly;

                try
                {
                    assembly = Assembly.LoadFile(dll);
                }
                catch (Exception e)
                {
                    Logger.Log(LogLevel.Error, $"Failed to load {dll}: {e.Message}");
                    if (e.InnerException != null)
                        Logger.Log(LogLevel.Error, $"Inner: {e.InnerException}");
                    continue;
                }

                foreach (Type type in assembly.GetTypes())
                {
                    if (type.IsInterface)
                        continue;

                    FieldInfo targetAssemblyNamesField = type.GetField("TargetAssemblyNames", BindingFlags.Static | BindingFlags.Public);

                    if (targetAssemblyNamesField == null || targetAssemblyNamesField.FieldType != typeof(string[]))
                        continue;

                    MethodInfo patchMethod = type.GetMethod("Patch", BindingFlags.Static | BindingFlags.Public);

                    if (patchMethod == null || patchMethod.ReturnType != typeof(void))
                        continue;

                    ParameterInfo[] parameters = patchMethod.GetParameters();

                    if (parameters.Length != 1 || parameters[0].ParameterType != typeof(AssemblyDefinition))
                        continue;

                    string[] requestedAssemblies = targetAssemblyNamesField.GetValue(null) as string[];

                    if (requestedAssemblies == null || requestedAssemblies.Length == 0)
                        continue;

                    Logger.Log(LogLevel.Info, $"Adding {type.FullName}");

                    foreach (string requestedAssembly in requestedAssemblies)
                    {
                        if (!patchersDictionary.TryGetValue(requestedAssembly, out List<MethodInfo> list))
                        {
                            list = new List<MethodInfo>();
                            patchersDictionary.Add(requestedAssembly, list);
                        }

                        list.Add(patchMethod);
                    }
                }
            }
        }

        /// <summary>
        ///     Carry out patching on the asemblies.
        /// </summary>
        public static void Patch()
        {
            Logger.Log(LogLevel.Info, "Patching assemblies:");

            foreach (KeyValuePair<string, List<MethodInfo>> patchJob in patchersDictionary)
            {
                string assemblyName = patchJob.Key;
                List<MethodInfo> patchers = patchJob.Value;

                string assemblyPath = Path.Combine(Utils.GameAssembliesDir, assemblyName);

                if (!File.Exists(assemblyPath))
                {
                    Logger.Log(LogLevel.Warning, $"{assemblyPath} does not exist. Skipping...");
                    continue;
                }

                AssemblyDefinition assemblyDefinition;

                try
                {
                    assemblyDefinition = AssemblyDefinition.ReadAssembly(assemblyPath);
                }
                catch (Exception e)
                {
                    Logger.Log(LogLevel.Error, $"Failed to open {assemblyPath}: {e.Message}");
                    continue;
                }

                foreach (MethodInfo patcher in patchers)
                {
                    Logger.Log(LogLevel.Info, $"Running {patcher.DeclaringType.FullName}");
                    try
                    {
                        patcher.Invoke(null, new object[] {assemblyDefinition});
                    }
                    catch (TargetInvocationException te)
                    {
                        Exception inner = te.InnerException;
                        if (inner != null)
                        {
                            Logger.Log(LogLevel.Error, $"Error inside the patcher: {inner.Message}");
                            Logger.Log(LogLevel.Error, $"Stack trace:\n{inner.StackTrace}");
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.Log(LogLevel.Error, $"By the patcher loader: {e.Message}");
                        Logger.Log(LogLevel.Error, $"Stack trace:\n{e.StackTrace}");
                    }
                }

                MemoryStream ms = new MemoryStream();

                // Write the patched assembly into memory
                assemblyDefinition.Write(ms);
                assemblyDefinition.Dispose();

                byte[] assemblyBytes = ms.ToArray();

                // Save the patched assembly to a file for debugging purposes
                SavePatchedAssembly(assemblyBytes, Path.GetFileNameWithoutExtension(assemblyName));

                // Load the patched assembly directly from memory
                // Since .NET loads all assemblies only once,
                // any further attempts by Unity to load the patched assemblies
                // will do nothing. Thus we achieve the same "dynamic patching" effect as with Sybaris.
                Assembly.Load(ms.ToArray());

                ms.Dispose();
            }
        }

        /// <summary>
        ///     The entry point of the loader
        /// </summary>
        public static void Main()
        {
            if (!Directory.Exists(Utils.SybarisDir))
                Directory.CreateDirectory(Utils.SybarisDir);

            Configuration.Init();

            if (!Configuration.Options["debug"]["logging"]["outputDirectory"].IsString)
                Configuration.Options["debug"]["logging"]["enabled"] = false;
            else if (!Directory.Exists(Configuration.Options["debug"]["logging"]["outputDirectory"]))
                Directory.CreateDirectory(Configuration.Options["debug"]["logging"]["outputDirectory"]);

            if (!Directory.Exists(Configuration.Options["debug"]["outputAssemblies"]["outputDirectory"]))
                Directory.CreateDirectory(Configuration.Options["debug"]["outputAssemblies"]["outputDirectory"]);

            if (Configuration.Options["debug"]["logging"]["enabled"])
                Logger.Enabled = true;
            if (Configuration.Options["debug"]["logging"]["redirectConsole"])
                Logger.RerouteStandardIO();

            Logger.Log("===Sybaris Loader===");
            Logger.Log($"Started on {DateTime.Now:R}");
            Logger.Log($"Game assembly directory: {Utils.GameAssembliesDir}");
            Logger.Log($"Sybaris directory: {Utils.SybarisDir}");

            // We add a custom assembly resolver
            // Since assemblies don't unload, this event handler will be called always there is an assembly to resolve
            // This allows us to put our patchers and plug-ins in our own folders.
            AppDomain.CurrentDomain.AssemblyResolve += ResolvePatchers;

            LoadPatchers();

            if (patchersDictionary.Count == 0)
            {
                Logger.Log(LogLevel.Info, "No valid patchers found! Quiting...");
                Logger.Dispose();
                return;
            }

            Patch();

            Logger.Log(LogLevel.Info, "Patching complete! Disposing of logger!");
            Logger.Dispose();
        }

        public static Assembly ResolvePatchers(object sender, ResolveEventArgs args)
        {
            // Try to resolve from patches directory
            if (Utils.TryResolveDllAssembly(args.Name, Utils.SybarisDir, out Assembly patchAssembly))
                return patchAssembly;
            return null;
        }

        private static void SavePatchedAssembly(byte[] assembly, string name)
        {
            if (!Configuration.Options["debug"]["outputAssemblies"]["enabled"]
                || !Configuration.Options["debug"]["outputAssemblies"]["outputDirectory"].IsString
                || Configuration.Options["debug"]["outputAssemblies"]["outputDirectory"] == null)
                return;

            string outDir = Configuration.Options["debug"]["outputAssemblies"]["outputDirectory"];

            string path = Path.Combine(outDir, $"{name}_patched.dll");

            if (!Directory.Exists(outDir))
                try
                {
                    Directory.CreateDirectory(outDir);
                }
                catch (Exception e)
                {
                    Logger.Log(LogLevel.Warning, $"Failed to create patched assembly directory to {outDir}!\nReason: {e.Message}");
                    return;
                }

            File.WriteAllBytes(path, assembly);

            Logger.Log(LogLevel.Info, $"Saved patched {name} to {path}");
        }
    }
}