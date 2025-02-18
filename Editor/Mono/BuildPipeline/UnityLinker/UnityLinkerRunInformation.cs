// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.UnityLinker;

namespace UnityEditorInternal
{
    class UnityLinkerRunInformation
    {
        public readonly string managedAssemblyFolderPath;
        public readonly BuildTarget target;
        public readonly BuildTargetGroup buildTargetGroup;
        public readonly BaseUnityLinkerPlatformProvider platformProvider;
        public readonly RuntimeClassRegistry rcr;
        public readonly ManagedStrippingLevel managedStrippingLevel;
        public readonly UnityLinkerArgumentValueProvider argumentProvider;
        public readonly bool engineStrippingSupported;
        public readonly bool isMonoBackend;
        public readonly bool performEngineStripping;
        public readonly IIl2CppPlatformProvider il2CppPlatformProvider;
        public readonly UnityLinkerBuildPipelineData pipelineData;
        public readonly BuildReport buildReport;

        public UnityLinkerRunInformation(string managedAssemblyFolderPath,
                                         BaseUnityLinkerPlatformProvider platformProvider, BuildTarget buildTarget,
                                         RuntimeClassRegistry rcr, ManagedStrippingLevel managedStrippingLevel,
                                         IIl2CppPlatformProvider il2CppPlatformProvider, BuildReport buildReport = null)
        {
            this.managedAssemblyFolderPath = managedAssemblyFolderPath;
            target = buildTarget;
            this.platformProvider = platformProvider;
            this.rcr = rcr;
            this.managedStrippingLevel = managedStrippingLevel;
            this.il2CppPlatformProvider = il2CppPlatformProvider;
            this.buildReport = buildReport;
            pipelineData = new UnityLinkerBuildPipelineData(target, managedAssemblyFolderPath);

            buildTargetGroup = BuildPipeline.GetBuildTargetGroup(buildTarget);
            argumentProvider = new UnityLinkerArgumentValueProvider(this);
            isMonoBackend = PlayerSettings.GetScriptingBackend(buildTargetGroup) == ScriptingImplementation.Mono2x;
            engineStrippingSupported = (platformProvider?.supportsEngineStripping ?? false) && !isMonoBackend;
            performEngineStripping = rcr != null && PlayerSettings.stripEngineCode && engineStrippingSupported;
        }

        public string ModulesAssetFilePath => platformProvider.modulesAssetFile;

        // *begin-nonstandard-formatting*
        public BuildReport BuildReport => buildReport ?? il2CppPlatformProvider?.buildReport;
        // *end-nonstandard-formatting*

        public StrippingInfo BuildReportData => BuildReport == null ? null : StrippingInfo.GetBuildReportData(BuildReport);

        public List<string> GetUserAssemblies()
        {
            return rcr.GetUserAssemblies().Where(s => rcr.IsDLLUsed(s)).Select(s => Path.Combine(managedAssemblyFolderPath, s)).ToList();
        }

        public List<string> AssembliesToProcess()
        {
            var userAssemblies = GetUserAssemblies();
            userAssemblies.AddRange(Directory.GetFiles(managedAssemblyFolderPath, "I18N*.dll", SearchOption.TopDirectoryOnly));
            return userAssemblies;
        }

        public string EditorToLinkerDataPath => Path.Combine(managedAssemblyFolderPath, "EditorToUnityLinkerData.json");

        public IEnumerable<string> SearchDirectories
        {
            get { yield return managedAssemblyFolderPath; }
        }
    }
}
