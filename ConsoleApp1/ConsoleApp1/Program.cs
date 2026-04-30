using System.CommandLine;
using System.IO;

// --- הגדרת אופציות עבור פקודת ה-Bundle ---
var languageOption = new Option<string[]>(new[] { "--language", "-l" }, "List of languages or 'all'") { IsRequired = true };
var outputOption = new Option<FileInfo>(new[] { "--output", "-o" }, "Output file path");
var noteOption = new Option<bool>(new[] { "--note", "-n" }, "Include source file paths");
var sortOption = new Option<string>(new[] { "--sort", "-s" }, "Sort by 'name' or 'type'");
sortOption.SetDefaultValue("name");
var removeEmptyLinesOption = new Option<bool>(new[] { "--remove-empty-lines", "-r" }, "Remove empty lines");
var authorOption = new Option<string>(new[] { "--author", "-a" }, "Author name");

var bundleCommand = new Command("bundle", "Bundle code files into a single file");
bundleCommand.AddOption(languageOption);
bundleCommand.AddOption(outputOption);
bundleCommand.AddOption(noteOption);
bundleCommand.AddOption(sortOption);
bundleCommand.AddOption(removeEmptyLinesOption);
bundleCommand.AddOption(authorOption);

bundleCommand.SetHandler((string[] languages, FileInfo output, bool note, string sort, bool removeLines, string author) =>
{
    try
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        var allFiles = Directory.GetFiles(currentDirectory, "*.*", SearchOption.AllDirectories);

        // --- שינוי 1: סינון תיקיות נעולות/מערכת ---
        var filteredFiles = allFiles.Where(file =>
        {
            string[] excludedFolders = { "bin", "debug", "obj", ".vs", ".git" };
            // שימוש ב-Split ו-Contains מבטיח שנחפש תיקייה שלמה ולא רק חלק משם
            var pathParts = file.Split(Path.DirectorySeparatorChar);
            return !excludedFolders.Any(folder => pathParts.Contains(folder));
        }).ToList();

        // --- שינוי 2: סינון לפי שפות על הרשימה המסוננת כבר ---
        // סינון לפי שפות - מוודא שאנחנו לוקחים רק קבצי טקסט מוכרים
        var finalFiles = filteredFiles.Where(file =>
        {
            // רשימת סיומות של קבצי קוד שאנחנו מרשים
            string[] allowedExtensions = { ".cs", ".py", ".java", ".txt", ".js", ".ts", ".html", ".css" };
            string extension = Path.GetExtension(file).ToLower();

            if (languages.Contains("all"))
            {
                // אם המשתמש בחר 'all', ניקח רק קבצים מסוגי הקוד המוכרים כדי למנוע ג'יבריש
                return allowedExtensions.Contains(extension);
            }

            // אם המשתמש בחר שפות ספציפיות (למשל 'py'), נבדוק אם הסיומת מתאימה
            return languages.Any(lang => extension == "." + lang.Trim().ToLower());
        }).ToList();

        // מיון הקבצים
        if (sort == "type")
            finalFiles = finalFiles.OrderBy(f => Path.GetExtension(f)).ThenBy(f => Path.GetFileName(f)).ToList();
        else
            finalFiles = finalFiles.OrderBy(f => Path.GetFileName(f)).ToList();

        using (var writer = new StreamWriter(output.FullName))
        {
            if (!string.IsNullOrWhiteSpace(author))
            {
                writer.WriteLine($"// Author: {author}");
            }

            foreach (var file in finalFiles)
            {
                try
                {
                    // --- שינוי 3: איחוד הקריאה כדי למנוע כפילות ---
                    if (note)
                    {
                        writer.WriteLine($"// Source: {Path.GetRelativePath(currentDirectory, file)}");
                    }

                    var lines = File.ReadAllLines(file);
                    foreach (var line in lines)
                    {
                        if (removeLines && string.IsNullOrWhiteSpace(line))
                            continue;

                        writer.WriteLine(line);
                    }
                    writer.WriteLine("------------------------------------------");
                }
                catch (IOException) // טיפול בקובץ נעול
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"[Warning] Skipping locked file: {Path.GetFileName(file)}");
                    Console.ResetColor();
                }
            }
        }

        Console.WriteLine($"Successfully bundled {finalFiles.Count} files into {output.Name}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"An error occurred: {ex.Message}");
    }

}, languageOption, outputOption, noteOption, sortOption, removeEmptyLinesOption, authorOption);

// --- פקודת ה-Create-Rsp (נשארה זהה, רק הוספת מירכאות ב-Author) ---
var createRspCommand = new Command("create-rsp", "Create a response file for the bundle command");
createRspCommand.SetHandler(() =>
{
    Console.WriteLine("Creating a new response file...");
    Console.Write("Enter languages (e.g., 'cs, py' or 'all'): ");
    string languages = Console.ReadLine() ?? "all";

    Console.Write("Enter output file path: ");
    var output = Console.ReadLine();

    Console.Write("Include source notes? (y/n): ");
    var note = Console.ReadLine()?.ToLower() == "y";

    Console.Write("Sort by (name/type): ");
    var sort = Console.ReadLine() == "type" ? "type" : "name";

    Console.Write("Remove empty lines? (y/n): ");
    var removeLines = Console.ReadLine()?.ToLower() == "y";

    Console.Write("Enter author name: ");
    var author = Console.ReadLine();

    var rspContent = $"bundle -l {languages}";
    if (!string.IsNullOrEmpty(output)) rspContent += $" -o \"{output}\"";
    if (note) rspContent += " -n";
    rspContent += $" -s {sort}";
    if (removeLines) rspContent += " -r";
    if (!string.IsNullOrEmpty(author)) rspContent += $" -a \"{author}\"";

    File.WriteAllText("bundle.rsp", rspContent);
    Console.WriteLine("\nSuccess! 'bundle.rsp' created.");
});

var rootCommand = new RootCommand("CLI Tool for File Bundling");
rootCommand.AddCommand(bundleCommand);
rootCommand.AddCommand(createRspCommand);

await rootCommand.InvokeAsync(args);