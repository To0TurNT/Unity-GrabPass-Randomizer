// ============================================================================
//  GrabPass Randomizer - TurNT_
// ============================================================================
//
//  This started as a gift. I wrote it for a "friend", stuffed it full of
//  jokes, and handed it over. He is now selling it for
//  $15 with "made my own that I could sell, don't worry didn't skid anything".
//
//  What actually happened is my file went into GPT with "remove all references
//  to TurNT and re-word it". That works on names. It does not work on jokes,
//  because the model keeps the shape of whatever you feed it.
//
//  His version ships a disclaimer at the bottom promising that no shaders were
//  "uploaded, stolen, blessed, cursed, or emailed" during the process.
//
//  Read that again. Nothing in his tool uploads. Nothing emails. Nothing
//  blesses. So why does it need to deny doing any of it?
//
//  Because mine did. The Blessing Level slider was mine - 0.00 to 1.00, told
//  you TurNT was pleased with your dedication. The fake "auto-upload to
//  TurNT's secret GitHub" toggle was mine. The "send Unity package to
//  TurNT@shaderthief.com" toggle was mine. He kept the punchline to a joke he
//  never wrote the setup for, and he is charging fifteen dollars for it.
//
//  Feeding someone else's work to an LLM does not make it yours. It makes it
//  their work with the serial numbers filed off, and you can always see the
//  filing marks.
//
//  Here is the actual tool, for free, doing the one thing it was ever meant to
//  do. If you paid for this, go get your money back.
// ============================================================================
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;
using System.IO;

public class TurNT_GrabPassRandomizer : EditorWindow
{
    private ShaderEntry[] allShaders;
    private ShaderEntry[] shaders;
    private string[] shaderNames;
    private int selectedShaderIndex = 0;
    private string searchFilter = "";
    private float fittedHeight;

    private static readonly Regex GrabPassRegex = new Regex(@"(?<!\w)GrabPass\s*\{");
    private static readonly Regex TagsRegex = new Regex(@"(?<!\w)Tags\s*\{[^}]*\}");
    private static readonly Regex NameRegex = new Regex("(?<!\\w)Name\\s*\"[^\"]*\"");
    private static readonly Regex QuotedRegex = new Regex("\"([^\"]+)\"");
    private static readonly Regex ShaderDeclRegex = new Regex("Shader\\s+\"([^\"]+)\"");
    private static readonly Regex IncludeRegex = new Regex("#include\\s+\"([^\"]+)\"");

    private class ShaderEntry
    {
        public readonly Shader Asset;
        public readonly string AssetPath;
        public readonly string Name;

        public ShaderEntry(Shader asset, string assetPath)
        {
            Asset = asset;
            AssetPath = assetPath;
            Name = asset == null ? "" : asset.name;
        }

        public bool Matches(string filter)
        {
            return Name.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) >= 0
                || AssetPath.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

    [MenuItem("TurNT_Tools/GrabPass Randomizer")]
    public static void ShowWindow()
    {
        GetWindow<TurNT_GrabPassRandomizer>("GrabPass Randomizer").LoadShaders();
    }

    private void LoadShaders()
    {
        allShaders = AssetDatabase.FindAssets("t:Shader")
            .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
            .Where(path => path.EndsWith(".shader", System.StringComparison.OrdinalIgnoreCase))
            .Select(path => new ShaderEntry(AssetDatabase.LoadAssetAtPath<Shader>(path), path))
            .Where(e => e.Asset != null)
            .OrderBy(e => e.Name)
            .ToArray();
        ApplyFilter();
    }

    private void ReloadShaders()
    {
        LoadShaders();
        Debug.Log("GrabPass Randomizer: reloaded " + allShaders.Length +
                  (allShaders.Length == 1 ? " shader." : " shaders."));
    }

    private void ApplyFilter()
    {
        string previous = (shaders != null && selectedShaderIndex < shaders.Length)
            ? shaders[selectedShaderIndex].Name
            : null;

        shaders = string.IsNullOrEmpty(searchFilter)
            ? allShaders
            : allShaders.Where(e => e.Matches(searchFilter)).ToArray();
        shaderNames = shaders.Select(e => e.Name).ToArray();

        int index = previous == null ? -1 : System.Array.FindIndex(shaders, e => e.Name == previous);
        selectedShaderIndex = index >= 0 ? index : 0;
    }

    private void OnGUI()
    {
        EditorGUILayout.BeginVertical();
        DrawBody();
        EditorGUILayout.EndVertical();
        FitHeightToContent();
    }

    private void DrawBody()
    {
        GUILayout.Label("Made by: TurNT_", EditorStyles.boldLabel);

        if (allShaders == null || allShaders.Length == 0)
        {
            GUILayout.Label("No shaders found. Try reloading.");
            if (GUILayout.Button("Reload Shaders")) ReloadShaders();
            return;
        }

        EditorGUI.BeginChangeCheck();
        searchFilter = EditorGUILayout.TextField("Search", searchFilter);
        if (EditorGUI.EndChangeCheck()) ApplyFilter();

        if (shaders.Length == 0)
        {
            EditorGUILayout.HelpBox("No shaders match the search.", MessageType.Info);
            if (GUILayout.Button("Reload Shaders")) ReloadShaders();
            return;
        }

        selectedShaderIndex = EditorGUILayout.Popup("Shader", selectedShaderIndex, shaderNames);

        if (GUILayout.Button("Apply Changes"))
        {
            DuplicateAndModifyShader(shaders[selectedShaderIndex].Asset);
        }

        if (GUILayout.Button("Reload Shaders")) ReloadShaders();
    }

    private void FitHeightToContent()
    {
        if (Event.current.type != EventType.Repaint) return;

        float height = GUILayoutUtility.GetLastRect().yMax + 4f;
        if (height <= 1f || Mathf.Abs(height - fittedHeight) < 1f) return;

        fittedHeight = height;
        minSize = new Vector2(300f, height);
        maxSize = new Vector2(4000f, height);
    }

    private void DuplicateAndModifyShader(Shader shader)
    {
        string shaderPath = AssetDatabase.GetAssetPath(shader);
        if (string.IsNullOrEmpty(shaderPath) || !shaderPath.EndsWith(".shader"))
        {
            Debug.LogError("Invalid shader file!");
            return;
        }

        string directory = Path.GetDirectoryName(shaderPath);
        string originalShaderName = Path.GetFileNameWithoutExtension(shaderPath);
        string shaderContent = File.ReadAllText(shaderPath);

        List<string> grabPassNames = FindGrabPassNames(shaderContent);
        if (grabPassNames.Count == 0)
        {
            Debug.LogError("No named GrabPass found in " + shader.name + ". Nothing to randomize.");
            return;
        }

        string suffix = null;
        string newShaderPath = null;
        for (int attempt = 0; attempt < 32 && newShaderPath == null; attempt++)
        {
            string candidateSuffix = NewSuffix();
            string candidatePath = Path.Combine(directory, originalShaderName + "_" + candidateSuffix + ".shader").Replace('\\', '/');
            if (File.Exists(candidatePath)) continue;

            suffix = candidateSuffix;
            newShaderPath = candidatePath;
        }

        if (newShaderPath == null)
        {
            Debug.LogError("Could not find an unused suffix for " + originalShaderName + ".");
            return;
        }

        foreach (string grabPassName in grabPassNames)
        {
            shaderContent = RenameIdentifier(shaderContent, grabPassName, grabPassName + "_" + suffix);
        }

        shaderContent = SuffixShaderName(shaderContent, suffix);

        File.WriteAllText(newShaderPath, shaderContent);
        AssetDatabase.Refresh();
        EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Shader>(newShaderPath));

        Debug.Log("GrabPass randomized: " + newShaderPath + "\n" +
                  string.Join("\n", grabPassNames.Select(n => n + " -> " + n + "_" + suffix).ToArray()));

        WarnAboutIncludes(shaderPath, shaderContent, grabPassNames);
    }

    private static List<string> FindGrabPassNames(string source)
    {
        string masked = MaskComments(source);
        var names = new List<string>();

        foreach (Match block in GrabPassRegex.Matches(masked))
        {
            int open = block.Index + block.Length - 1;
            int close = FindMatchingBrace(masked, open);
            if (close < 0) continue;

            string body = masked.Substring(open + 1, close - open - 1);
            body = TagsRegex.Replace(body, " ");
            body = NameRegex.Replace(body, " ");

            Match quoted = QuotedRegex.Match(body);
            if (quoted.Success && !names.Contains(quoted.Groups[1].Value))
            {
                names.Add(quoted.Groups[1].Value);
            }
        }
        return names;
    }

    private static int FindMatchingBrace(string source, int openIndex)
    {
        int depth = 0;
        for (int i = openIndex; i < source.Length; i++)
        {
            if (source[i] == '{') depth++;
            else if (source[i] == '}' && --depth == 0) return i;
        }
        return -1;
    }

    private static string MaskComments(string source)
    {
        var masked = new System.Text.StringBuilder(source);

        for (int i = 0; i < source.Length;)
        {
            if (source[i] == '"')
            {
                for (i++; i < source.Length && source[i] != '"' && source[i] != '\n'; i++) { }
                i++;
            }
            else if (source[i] == '/' && i + 1 < source.Length && source[i + 1] == '/')
            {
                for (; i < source.Length && source[i] != '\n'; i++) masked[i] = ' ';
            }
            else if (source[i] == '/' && i + 1 < source.Length && source[i + 1] == '*')
            {
                masked[i++] = ' ';
                for (; i < source.Length; i++)
                {
                    bool end = source[i] == '*' && i + 1 < source.Length && source[i + 1] == '/';
                    if (source[i] != '\n') masked[i] = ' ';
                    if (end) { masked[++i] = ' '; i++; break; }
                }
            }
            else i++;
        }

        return masked.ToString();
    }

    private static string RenameIdentifier(string source, string oldName, string newName)
    {
        return Regex.Replace(source, @"(?<!\w)" + Regex.Escape(oldName) + @"(?!\w)", newName.Replace("$", "$$"));
    }

    private static string SuffixShaderName(string source, string suffix)
    {
        Match match = ShaderDeclRegex.Match(source);
        if (!match.Success) return source;

        Group group = match.Groups[1];
        string[] parts = group.Value.Split('/');
        parts[parts.Length - 1] = parts[parts.Length - 1] + "_" + suffix;

        return source.Remove(group.Index, group.Length).Insert(group.Index, string.Join("/", parts));
    }

    private static void WarnAboutIncludes(string shaderPath, string source, List<string> grabPassNames)
    {
        string directory = Path.GetDirectoryName(shaderPath);

        foreach (Match match in IncludeRegex.Matches(source))
        {
            string relative = match.Groups[1].Value;
            string path = (relative.StartsWith("Assets/") || relative.StartsWith("Packages/"))
                ? relative
                : Path.Combine(directory, relative);

            if (!File.Exists(path)) continue;

            string text = File.ReadAllText(path);
            foreach (string name in grabPassNames)
            {
                if (Regex.IsMatch(text, @"(?<!\w)" + Regex.Escape(name) + @"(?!\w)"))
                {
                    Debug.LogWarning("'" + name + "' is also referenced in " + relative +
                                     ". The duplicate still includes the original file, so the grab will not bind. " +
                                     "Move the declaration into the shader or randomize the include too.");
                    break;
                }
            }
        }
    }

    private static string NewSuffix()
    {
        return Random.Range(0, 1000000).ToString("D6");
    }
}
#endif