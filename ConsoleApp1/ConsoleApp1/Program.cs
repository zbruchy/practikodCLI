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

// --- פקודת ה-Bundle (הלוגיקה הקיימת) ---
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
        // 1. קבלת כל הקבצים בתיקייה הנוכחית (כולל תיקיות משנה)
        var currentDirectory = Directory.GetCurrentDirectory();
        var allFiles = Directory.GetFiles(currentDirectory, "*.*", SearchOption.AllDirectories);

        // 2. סינון קבצים: לפי שפות ולפי נתיבים אסורים (bin, debug)
        var filteredFiles = allFiles.Where(file =>
        {
            // בדיקה אם הקובץ בתיקייה אסורה
            bool isForbiddenPath = file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") ||
                                   file.Contains($"{Path.DirectorySeparatorChar}debug{Path.DirectorySeparatorChar}");
            if (isForbiddenPath) return false;

            // בדיקת סיומת הקובץ מול רשימת השפות
            string extension = Path.GetExtension(file).TrimStart('.');
            return languages.Contains("all") || languages.Contains(extension);
        }).ToList();

        // 3. מיון הקבצים
        if (sort == "type")
            filteredFiles = filteredFiles.OrderBy(f => Path.GetExtension(f)).ThenBy(f => Path.GetFileName(f)).ToList();
        else
            filteredFiles = filteredFiles.OrderBy(f => Path.GetFileName(f)).ToList();

        // 4. כתיבה לקובץ הפלט
        using (var writer = new StreamWriter(output.FullName))
        {
            // הוספת שם המחבר בראש הקובץ אם צוין
            if (!string.IsNullOrWhiteSpace(author))
            {
                writer.WriteLine($"// Author: {author}");
            }

            foreach (var file in filteredFiles)
            {
                // הוספת הערה עם נתיב הקובץ המקורי
                if (note)
                {
                    writer.WriteLine($"// Source: {Path.GetRelativePath(currentDirectory, file)}");
                }

                // קריאת תוכן הקובץ
                var lines = File.ReadAllLines(file);
                foreach (var line in lines)
                {
                    // מחיקת שורות ריקות אם המשתמש ביקש
                    if (removeLines && string.IsNullOrWhiteSpace(line))
                        continue;

                    writer.WriteLine(line);
                }
                writer.WriteLine(); // שורה ריקה בין קובץ לקובץ
            }
        }

        Console.WriteLine($"Successfully bundled {filteredFiles.Count} files into {output.Name}");
    }
    catch (DirectoryNotFoundException)
    {
        Console.WriteLine("Error: The output directory is invalid.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"An error occurred: {ex.Message}");
    }

}, languageOption, outputOption, noteOption, sortOption, removeEmptyLinesOption, authorOption);

// --- פקודת ה-Create-Rsp (הפיצ'ר החדש) ---
var createRspCommand = new Command("create-rsp", "Create a response file for the bundle command");

createRspCommand.SetHandler(() =>
{
    Console.WriteLine("Creating a new response file. Please answer the following questions:");

    // 1. ולידציה על שפות
    string languages = "";
    while (string.IsNullOrWhiteSpace(languages))
    {
        Console.Write("Enter languages (e.g., 'cs, py' or 'all'): ");
        languages = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(languages)) Console.WriteLine("Error: Language is required!");
    }

    // 2. קלט לנתיב פלט
    Console.Write("Enter output file path (e.g., bundle.txt): ");
    var output = Console.ReadLine();

    // 3. שאלות כן/לא (bool)
    Console.Write("Include source notes? (y/n): ");
    var note = Console.ReadLine()?.ToLower() == "y";

    Console.Write("Sort by (name/type): ");
    var sort = Console.ReadLine();
    if (sort != "type") sort = "name";

    Console.Write("Remove empty lines? (y/n): ");
    var removeLines = Console.ReadLine()?.ToLower() == "y";

    Console.Write("Enter author name (optional): ");
    var author = Console.ReadLine();

    // בניית פקודת ה-Response
    // אנחנו בונים מחרוזת שנראית בדיוק כמו פקודה בטרמינל
    var rspContent = $"bundle -l {languages}";
    if (!string.IsNullOrEmpty(output)) rspContent += $" -o \"{output}\"";
    if (note) rspContent += " -n";
    rspContent += $" -s {sort}";
    if (removeLines) rspContent += " -r";
    if (!string.IsNullOrEmpty(author)) rspContent += $" -a \"{author}\"";

    try
    {
        File.WriteAllText("bundle.rsp", rspContent);
        Console.WriteLine("\nSuccess! 'bundle.rsp' created.");
        Console.WriteLine("To run it, use: dotnet @bundle.rsp");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error creating file: {ex.Message}");
    }
});

// --- הגדרת פקודת השורש והרצה ---
var rootCommand = new RootCommand("CLI Tool for File Bundling");
rootCommand.AddCommand(bundleCommand);
rootCommand.AddCommand(createRspCommand); // הוספת הפקודה החדשה לעץ הפקודות

await rootCommand.InvokeAsync(args);
