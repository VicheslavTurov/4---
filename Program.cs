using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace TextEditorLab
{
// Представляет структуру документа и управляет его сохранением/загрузкой.
// Отделение данных от логики упрощает сериализацию и тестирование.
public class TextDocument
{
    public string FileName { get; set; }
    public string Content { get; set; }

    public TextDocument()
    {
        FileName = "Untitled.txt";
        Content = string.Empty;
    }

    public TextDocument(string name, string text)
    {
        FileName = name;
        Content = text;
    }

    // XML-сериализация даёт читаемый формат для сохранённых файлов.
    public void SaveToXml(string path)
    {
        var formatter = new XmlSerializer(typeof(TextDocument));
        using (var fs = new FileStream(path, FileMode.Create))
            formatter.Serialize(fs, this);
    }

    public static TextDocument LoadFromXml(string path)
    {
        var formatter = new XmlSerializer(typeof(TextDocument));
        using (var fs = new FileStream(path, FileMode.Open))
            return (TextDocument)formatter.Deserialize(fs);
    }

    // Бинарный формат компактнее и удобнее для больших объёмов текста.
    public void SaveToBinary(string path)
    {
        using (var writer = new BinaryWriter(File.Open(path, FileMode.Create)))
        {
            writer.Write(FileName ?? string.Empty);
            writer.Write(Content ?? string.Empty);
        }
    }

    public static TextDocument LoadFromBinary(string path)
    {
        using (var reader = new BinaryReader(File.Open(path, FileMode.Open)))
        {
            string loadedName = reader.ReadString();
            string loadedText = reader.ReadString();
            return new TextDocument(loadedName, loadedText);
        }
    }
}

// Снимок (Memento) фиксирует внутреннее состояние документа.
// Неизменяемость гарантирует, что история не будет случайно испорчена.
public class EditorMemento
{
    public string MementoContent { get; }

    public EditorMemento(string text)
    {
        MementoContent = text;
    }
}

// Управляет пользовательским интерфейсом и историей изменений.
public class ConsoleEditor
{
    private TextDocument _currentDoc;
    private Stack<EditorMemento> _undoStack;

    public ConsoleEditor()
    {
        _currentDoc = new TextDocument();
        _undoStack = new Stack<EditorMemento>();
    }

    // Главный цикл взаимодействия с пользователем.
    public void Start()
    {
        bool active = true;
        while (active)
        {
            Console.Clear();
            Console.WriteLine($"ФАЙЛ: {_currentDoc.FileName}\n{_currentDoc.Content}\n" +
                                "---------------------------------------\n" +
                                "1: Добавить текст | 2: Отмена | 3: Сохранить XML | 4: Загрузить XML | 0: Выход");
            Console.Write("Выберите: ");

            string userChoice = Console.ReadLine();
            switch (userChoice)
            {
                case "1":
                    ProcessTextAddition();
                    break;
                case "2":
                    PerformUndo();
                    break;
                case "3":
                    _currentDoc.SaveToXml("data.xml");
                    break;
                case "4":
                    _currentDoc = TextDocument.LoadFromXml("data.xml");
                    _undoStack.Clear(); // после полной замены документа история сбрасывается
                    break;
                case "0":
                    active = false;
                    break;
            }
        }
    }

    // Сохраняет текущее состояние в истории перед изменением.
    private void ProcessTextAddition()
    {
        Console.Write("Введите текст: ");
        string additionalText = Console.ReadLine();

        _undoStack.Push(new EditorMemento(_currentDoc.Content));
        _currentDoc.Content += additionalText + Environment.NewLine;
    }

    // Восстанавливает предыдущее состояние из стека истории.
    private void PerformUndo()
    {
        if (_undoStack.Count > 0)
            _currentDoc.Content = _undoStack.Pop().MementoContent;
    }
}

// Выполняет поиск и индексацию текстовых файлов в системе.
public class FileSearchIndexer
{
    private Dictionary<string, List<string>> _index;

    public FileSearchIndexer()
    {
        _index = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
    }

    // Инициализирует индекс и запускает рекурсивный обход папок.
    public void Index(string rootPath, string[] terms)
    {
        _index.Clear();
        foreach (string term in terms)
            _index[term] = new List<string>();

        WalkDirectory(rootPath, terms);
        DisplayResults();
    }

    // Рекурсивно обходит каталоги в поисках файлов с расширением .txt.
    private void WalkDirectory(string currentDir, string[] terms)
    {
        try
        {
            string[] txtFiles = Directory.GetFiles(currentDir, "*.txt");
            foreach (string file in txtFiles)
            {
                string fileContent = File.ReadAllText(file);
                foreach (string term in terms)
                {
                    if (fileContent.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
                        _index[term].Add(file);
                }
            }

            string[] subDirs = Directory.GetDirectories(currentDir);
            foreach (string dir in subDirs)
                WalkDirectory(dir, terms);
        }
        catch
        {
            // Пропускаем папки, к которым нет доступа, чтобы не крашить программу.
        }
    }

    // Выводит статистику и пути найденных файлов.
    private void DisplayResults()
    {
        string report = "\n--- РЕЗУЛЬТАТЫ ПОИСКА ---\n";
        foreach (var entry in _index)
        {
            report += $"Ключевое слово [{entry.Key}]: найдено в {entry.Value.Count} файлах.\n";
            for (int i = 0; i < entry.Value.Count; i++)
                report += $"  -> {entry.Value[i]}\n";
            report += "\n";
        }
        Console.WriteLine(report);
    }
}

// Точка входа.
class Program
{
    static void Main()
    {
        bool programRunning = true;
        while (programRunning)
        {
            Console.Clear();
            Console.WriteLine("1. Редактор\n2. Индексатор\n0. Выход");
            Console.Write("Ввод: ");

            string option = Console.ReadLine();
            if (option == "1")
            {
                var editor = new ConsoleEditor();
                editor.Start();
            }
            else if (option == "2")
            {
                RunSearch();
            }
            else if (option == "0")
            {
                programRunning = false;
            }
        }
    }

    // Запрашивает параметры поиска и запускает индексатор.
    private static void RunSearch()
    {
        Console.Write("Каталог: ");
        string searchRoot = Console.ReadLine();

        Console.Write("Ключевые слова (через запятую): ");
        string rawKeywords = Console.ReadLine();

        string[] keywordArray = rawKeywords.Split(',')
                                            .Select(s => s.Trim())
                                            .Where(s => !string.IsNullOrEmpty(s))
                                            .ToArray();

        var searcher = new FileSearchIndexer();
        searcher.Index(searchRoot, keywordArray);

        Console.WriteLine("Готово. Нажмите Enter для возврата в меню.");
        Console.ReadLine();
    }
}
}