// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using Mono.Cecil;
using UnityEditor.Scripting.ScriptCompilation;
using UnityEngine.Scripting;

namespace UnityEditor
{
    static class AssemblyValidation
    {
        // Keep in sync with AssemblyValidationFlags in MonoManager.cpp
        [Flags]
        public enum ErrorFlags
        {
            None = 0,
            ReferenceHasErrors = (1 << 0),
            UnresolvableReference = (1 << 1),
            IncompatibleWithEditor = (1 << 2),
            AsmdefPluginConflict = (1 << 3)
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Error
        {
            public ErrorFlags flags;
            public string message;
            public string assemblyPath;

            public void Add(ErrorFlags newFlags, string newMessage)
            {
                flags |= newFlags;

                if (message == null)
                {
                    message = newMessage;
                }
                else
                {
                    message += string.Format("\n{0}", newMessage);
                }
            }

            public bool HasFlag(ErrorFlags testFlags)
            {
                return (flags & testFlags) == testFlags;
            }

            public void ClearFlags(ErrorFlags clearFlags)
            {
                flags &= ~clearFlags;
            }
        }

        public struct AssemblyAndReferences
        {
            public int assemblyIndex;
            public int[] referenceIndicies;
        }

        class AssemblyResolver : BaseAssemblyResolver
        {
            readonly IDictionary<string, AssemblyDefinition> cache;

            public AssemblyResolver()
            {
                cache = new Dictionary<string, AssemblyDefinition>(StringComparer.Ordinal);
            }

            public override AssemblyDefinition Resolve(AssemblyNameReference name)
            {
                if (name == null)
                    throw new ArgumentNullException("name");

                AssemblyDefinition assembly;
                if (cache.TryGetValue(name.Name, out assembly))
                    return assembly;

                assembly = base.Resolve(name);
                cache[name.Name] = assembly;

                return assembly;
            }

            public void RegisterAssembly(AssemblyDefinition assembly)
            {
                if (assembly == null)
                    throw new ArgumentNullException("assembly");

                var name = assembly.Name.Name;
                if (cache.ContainsKey(name))
                    return;

                cache[name] = assembly;
            }

            protected override void Dispose(bool disposing)
            {
                foreach (var assembly in cache.Values)
                    assembly.Dispose();

                cache.Clear();

                base.Dispose(disposing);
            }
        }

        [RequiredByNativeCode]
        public static Error[] ValidateAssemblies(string[] assemblyPaths, bool enableLogging)
        {
            var searchPaths = AssemblyHelper.GetDefaultAssemblySearchPaths();

            var assemblyDefinitions = LoadAssemblyDefinitions(assemblyPaths, searchPaths);

            if (enableLogging)
            {
                // Prints assemblies and their references to the Editor.log
                PrintAssemblyDefinitions(assemblyDefinitions);

                foreach (var searchPath in searchPaths)
                {
                    Console.WriteLine("[AssemblyValidation] Search Path: '" + searchPath + "'");
                }
            }

            var errors = ValidateAssemblyDefinitions(assemblyPaths,
                assemblyDefinitions,
                PluginCompatibleWithEditor);

            return errors;
        }

        [RequiredByNativeCode]
        public static Error[] ValidateAssemblyDefinitionFiles()
        {
            var customScriptAssemblies = EditorCompilationInterface.Instance.GetCustomScriptAssemblies();

            if (customScriptAssemblies.Length == 0)
                return null;

            var pluginImporters = PluginImporter.GetAllImporters();

            if (pluginImporters == null || pluginImporters.Length == 0)
                return null;

            var pluginFilenameToAssetPath = new Dictionary<string, string>();

            foreach (var pluginImporter in pluginImporters)
            {
                var pluginAssetPath = pluginImporter.assetPath;
                var lowerPluginFilename = AssetPath.GetFileName(pluginAssetPath).ToLower(CultureInfo.InvariantCulture);
                pluginFilenameToAssetPath[lowerPluginFilename] = pluginAssetPath;
            }

            var errors = new List<Error>();

            foreach (var customScriptAssembly in customScriptAssemblies)
            {
                var lowerAsmdefFilename = $"{customScriptAssembly.Name.ToLower(CultureInfo.InvariantCulture)}.dll";

                string pluginPath;

                if (pluginFilenameToAssetPath.TryGetValue(lowerAsmdefFilename, out pluginPath))
                {
                    var error = new Error()
                    {
                        message = $"Plugin '{pluginPath}' has the same filename as Assembly Definition File '{customScriptAssembly.FilePath}'. Rename the assemblies to avoid hard to diagnose issues and crashes.",
                        flags = ErrorFlags.AsmdefPluginConflict,
                        assemblyPath = pluginPath
                    };

                    errors.Add(error);
                }
            }

            return errors.ToArray();
        }

        public static bool PluginCompatibleWithEditor(string path)
        {
            var pluginImporter = AssetImporter.GetAtPath(path) as PluginImporter;

            if (pluginImporter == null)
                return true;

            if (pluginImporter.GetCompatibleWithAnyPlatform())
                return true;

            return pluginImporter.GetCompatibleWithEditor();
        }

        public static bool ShouldValidateReferences(string path)
        {
            var pluginImporter = AssetImporter.GetAtPath(path) as PluginImporter;

            if (pluginImporter == null)
                return true;

            return pluginImporter.ValidateReferences;
        }

        public static void PrintAssemblyDefinitions(AssemblyDefinition[] assemblyDefinitions)
        {
            foreach (var assemblyDefinition in assemblyDefinitions)
            {
                Console.WriteLine("[AssemblyValidation] Assembly: " + assemblyDefinition.Name);

                var assemblyReferences = GetAssemblyNameReferences(assemblyDefinition);

                foreach (var reference in assemblyReferences)
                {
                    Console.WriteLine("[AssemblyValidation]   Reference: " + reference);
                }
            }
        }

        public static Error[] ValidateAssembliesInternal(string[] assemblyPaths,
            string[] searchPaths,
            Func<string, bool> compatibleWithEditor)
        {
            var assemblyDefinitions = LoadAssemblyDefinitions(assemblyPaths, searchPaths);
            return ValidateAssemblyDefinitions(assemblyPaths, assemblyDefinitions, compatibleWithEditor);
        }

        public static Error[] ValidateAssemblyDefinitions(string[] assemblyPaths,
            AssemblyDefinition[] assemblyDefinitions,
            Func<string, bool> compatibleWithEditor)
        {
            var errors = new Error[assemblyPaths.Length];

            CheckAssemblyReferences(assemblyPaths,
                errors,
                assemblyDefinitions,
                compatibleWithEditor);

            return errors;
        }

        public static AssemblyDefinition[] LoadAssemblyDefinitions(string[] assemblyPaths, string[] searchPaths)
        {
            var assemblyResolver = new AssemblyResolver();

            foreach (var asmpath in searchPaths)
                assemblyResolver.AddSearchDirectory(asmpath);

            var readerParameters = new ReaderParameters
            {
                AssemblyResolver = assemblyResolver
            };

            var assemblyDefinitions = new AssemblyDefinition[assemblyPaths.Length];

            for (int i = 0; i < assemblyPaths.Length; ++i)
            {
                assemblyDefinitions[i] = AssemblyDefinition.ReadAssembly(assemblyPaths[i], readerParameters);
                // Cecil tries to resolve references by filename, since Unity force loads
                // assemblies, then assembly reference will resolve even if the assembly name
                // does not match the assembly filename. So we register all assemblies in
                // in the resolver.
                assemblyResolver.RegisterAssembly(assemblyDefinitions[i]);
            }

            return assemblyDefinitions;
        }

        public static void CheckAssemblyReferences(string[] assemblyPaths,
            Error[] errors,
            AssemblyDefinition[] assemblyDefinitions,
            Func<string, bool> compatibleWithEditor)
        {
            SetupEditorCompatibility(assemblyPaths, errors, compatibleWithEditor);

            var assemblyDefinitionNameToIndex = new Dictionary<string, int>();
            var assembliesAndReferencesArray = new AssemblyAndReferences[assemblyPaths.Length];

            for (int i = 0; i < assemblyDefinitions.Length; ++i)
            {
                assemblyDefinitionNameToIndex[assemblyDefinitions[i].Name.Name] = i;
                assembliesAndReferencesArray[i] = new AssemblyAndReferences
                {
                    assemblyIndex = i,
                    referenceIndicies = new int[0]
                };
            }

            for (int i = 0; i < assemblyPaths.Length; ++i)
            {
                if (errors[i].HasFlag(ErrorFlags.IncompatibleWithEditor))
                    continue;

                var assemblyPath = assemblyPaths[i];

                // Check if "Validate References" option is enabled
                // in the PluginImporter
                if (!ShouldValidateReferences(assemblyPath))
                    continue;

                ResolveAndSetupReferences(i,
                    errors,
                    assemblyDefinitions,
                    assemblyDefinitionNameToIndex,
                    assembliesAndReferencesArray,
                    assemblyPath);
            }

            // Check assemblies for references to assemblies with errors
            int referenceErrorCount;

            do
            {
                referenceErrorCount = 0;

                foreach (var assemblyAndReferences in assembliesAndReferencesArray)
                {
                    var assemblyIndex = assemblyAndReferences.assemblyIndex;

                    foreach (var referenceIndex in assemblyAndReferences.referenceIndicies)
                    {
                        var referenceError = errors[referenceIndex];
                        if (errors[assemblyIndex].flags == ErrorFlags.None &&
                            referenceError.flags != ErrorFlags.None)
                        {
                            if (referenceError.HasFlag(ErrorFlags.IncompatibleWithEditor))
                            {
                                errors[assemblyIndex].Add(ErrorFlags.ReferenceHasErrors | ErrorFlags.IncompatibleWithEditor,
                                    string.Format("Reference '{0}' is incompatible with the editor.",
                                        assemblyDefinitions[referenceIndex].Name.Name));
                            }
                            else
                            {
                                errors[assemblyIndex].Add(ErrorFlags.ReferenceHasErrors | referenceError.flags,
                                    string.Format("Reference has errors '{0}'.",
                                        assemblyDefinitions[referenceIndex].Name.Name));
                            }

                            referenceErrorCount++;
                        }
                    }
                }
            }
            while (referenceErrorCount > 0);
        }

        public static void SetupEditorCompatibility(string[] assemblyPaths,
            Error[] errors,
            Func<string, bool> compatibleWithEditor)
        {
            for (int i = 0; i < assemblyPaths.Length; ++i)
            {
                var assemblyPath = assemblyPaths[i];

                if (!compatibleWithEditor(assemblyPath))
                {
                    errors[i].Add(ErrorFlags.IncompatibleWithEditor,
                        "Assembly is incompatible with the editor");
                }
            }
        }

        public static void ResolveAndSetupReferences(int index,
            Error[] errors,
            AssemblyDefinition[] assemblyDefinitions,
            Dictionary<string, int> assemblyDefinitionNameToIndex,
            AssemblyAndReferences[] assemblyAndReferences,
            string assemblyPath)
        {
            var assemblyDefinition = assemblyDefinitions[index];
            var assemblyResolver = assemblyDefinition.MainModule.AssemblyResolver;

            var assemblyReferences = GetAssemblyNameReferences(assemblyDefinition);

            var referenceIndieces = new List<int>
            {
                Capacity = assemblyReferences.Length
            };

            var assemblyVersionValidation = PlayerSettings.assemblyVersionValidation;

            bool isReferencingUnityAssemblies = false;
            foreach (var reference in assemblyReferences)
            {
                if (!isReferencingUnityAssemblies && (Utility.FastStartsWith(reference.Name, "UnityEngine.", "unityengine.") || Utility.FastStartsWith(reference.Name, "UnityEditor.", "unityeditor.")))
                {
                    isReferencingUnityAssemblies = true;
                }

                try
                {
                    var referenceAssemblyDefinition = assemblyResolver.Resolve(reference);

                    if (reference.Name == assemblyDefinition.Name.Name)
                    {
                        errors[index].Add(ErrorFlags.ReferenceHasErrors, $"{reference.Name} references itself.");
                    }

                    if (assemblyVersionValidation && assemblyDefinitionNameToIndex.TryGetValue(referenceAssemblyDefinition.Name.Name,
                        out int referenceAssemblyDefinitionIndex))
                    {
                        bool isSigned = IsSigned(reference);
                        if (isSigned)
                        {
                            var definition = assemblyDefinitions[referenceAssemblyDefinitionIndex];

                            if (definition.Name.Version.ToString() != reference.Version.ToString() && !IsInSameFolder(assemblyDefinition, referenceAssemblyDefinition))
                            {
                                errors[index].Add(ErrorFlags.UnresolvableReference,
                                    $"{assemblyDefinition.Name.Name} references strong named {reference.Name} Assembly references: {reference.Version} Found in project: {definition.Name.Version}.\nAssembly Version Validation can be disabled in Player Settings \"Assembly Version Validation\"");
                            }
                        }

                        referenceIndieces.Add(referenceAssemblyDefinitionIndex);
                    }
                }
                catch (AssemblyResolutionException)
                {
                    errors[index].Add(ErrorFlags.UnresolvableReference,
                        string.Format("Unable to resolve reference '{0}'. Is the assembly missing or incompatible with the current platform?\nReference validation can be disabled in the Plugin Inspector.",
                            reference.Name));
                }
            }

            if (isReferencingUnityAssemblies)
            {
                var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(assemblyPath);
                if (assemblyDefinitions[index].Name.Name != fileNameWithoutExtension)
                {
                    errors[index].Add(ErrorFlags.ReferenceHasErrors, $"Assembly name '{assemblyDefinitions[index].Name.Name}' does not match file name '{fileNameWithoutExtension}'");
                }
            }

            assemblyAndReferences[index].referenceIndicies = referenceIndieces.ToArray();
        }

        private static bool IsInSameFolder(AssemblyDefinition first, AssemblyDefinition second)
        {
            var firstAssemblyPath = Path.GetDirectoryName(first.MainModule.FileName);
            var secondAssemblyPath = Path.GetDirectoryName(second.MainModule.FileName);
            return firstAssemblyPath.Equals(secondAssemblyPath, StringComparison.Ordinal);
        }

        private static bool IsSigned(AssemblyNameReference reference)
        {
            //Bug in Cecil where HasPublicKey is always false
            foreach (var publicTokenByte in reference.PublicKeyToken)
            {
                if (publicTokenByte != 0)
                {
                    return true;
                }
            }
            return false;
        }

        public static AssemblyNameReference[] GetAssemblyNameReferences(AssemblyDefinition assemblyDefinition)
        {
            List<AssemblyNameReference> result = new List<AssemblyNameReference>
            {
                Capacity = 16
            };

            foreach (ModuleDefinition module in assemblyDefinition.Modules)
            {
                var references = module.AssemblyReferences;

                foreach (var reference in references)
                {
                    result.Add(reference);
                }
            }

            return result.ToArray();
        }
    }
}
