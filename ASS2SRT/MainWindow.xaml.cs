
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Navigation;

namespace ASS2SRT
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 
    public partial class MainWindow : Window
    {
        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        public class styleList
        {
            public int id { get; set; }
            public string name { get; set; }
        }

        public class Lines
        {
            public int order { get; set; }
            public DateTime start { get; set; }
            public DateTime end { get; set; }
            public string style { get; set; }
            public string actor { get; set; }
            public string phrase { get; set; }
            public string phraseWithActor()
            {
                return "[" + actor + "] " + phrase;
            }
            public override string ToString()
            {
                return phrase;
            }
            public string getLine(bool addActor)
            {


                string line = order.ToString() + "\n";
                string sTime = start.ToString("HH:mm:ss,fff");
                string eTime = end.ToString("HH:mm:ss,fff");

                line = line + sTime + " --> " + eTime + "\n";

                if (addActor && actor != "") line = line + phraseWithActor();
                else line = line + ToString();

                return line + "\n\n";
            }

        }

        public class SubClass
        {
            public string name { get; set; }
            public string originalName { get; set; }
            public string path { get; set; }
            public string content { get; set; }
            public List<Lines> lines { get; set; }
            public int linesCount { get; set; }
            public string epNumber { get; set; }
        }

        private string              _importPath, _exportPath, _fileName, _prio = "actor";
        private bool                _started = false, _keepOroginalNames = true,_addActorPhrase = false, _addActorFile = false, _addActorIgnoreFile = false, _addIgnoredStylesFile = false, _addSelectedStylesFile = false;
        private int                 _startNum = 1,_nullCount = 1,_countLines = 0,_countFiles = 0, _countStyles = 0, _thisLines = 0;
        private List<styleList>     _styles = new List<styleList>(), _stylesSelected = new List<styleList>(), _stylesIgnored = new List<styleList>();
        private List<string>        _actors = new List<string>(), _actorsSelected = new List<string>();
        private List<SubClass>      _subtitles = new List<SubClass>();

        public MainWindow()
        {
            InitializeComponent();
        }

        private void _setStatus(string status)
        {
            this.statusWord.Text = status;
        }

        private List<Lines> _getLines(string content)
        {
            //  Based on SubCSharp

            List<Lines> _phrases = new List<Lines>();

            using (StringReader assFile = new StringReader(content))
            {
                string line = "";

                while ((line = assFile.ReadLine()) != null)
                {
                    if (line.StartsWith("Dialogue:"))
                    {

                        

                        string[] splitLine = line.Split(",", 10, StringSplitOptions.None);
                        string text = splitLine[9].Replace("\\N", "\n");
                        string style = splitLine[3];
                        var actorName = splitLine[4];
                        Lines subline = new Lines();

                        subline.start = DateTime.ParseExact(splitLine[1], "H:mm:ss.ff", CultureInfo.InvariantCulture);
                        subline.end = DateTime.ParseExact(splitLine[2], "H:mm:ss.ff", CultureInfo.InvariantCulture);
                        subline.style = style;
                        subline.actor = actorName;
                        subline.phrase = text;

                        if (!_actors.Contains(actorName) && actorName != "") _actors.Add(actorName);
                        if (_phrases.Any(u => (u.phrase == subline.phrase && u.start == subline.start && u.end == subline.end)))
                            break;

                        _countLines++;
                        _thisLines++;
                        subline.order = _thisLines;

                        _phrases.Add(subline);
                    }
                }

            }

            return _phrases;
        }

        private string _aufrunden(int num)
        {
            string zahl = num.ToString();

            for (int i = zahl.Length; i < _nullCount; i++) zahl = "0" + zahl;

            return zahl;
        }

        private void GenSampleCount()
        {
            string zahl = _aufrunden(_startNum);
            if(sampleNum != null) sampleNum.Text = "This.AweSome.Sub." + zahl + ".srt";
        }

        private void nullCount_TextChanged(object sender, TextChangedEventArgs e)
        {
            _nullCount = Convert.ToInt32(nullCount.Text);
            GenSampleCount();
        }

        private void startNum_TextChanged(object sender, TextChangedEventArgs e)
        {
            _startNum = Convert.ToInt32(startNum.Text);
            GenSampleCount();
        }

        private void PrioButton_Checked(object sender, RoutedEventArgs e)
        {
            RadioButton check = (RadioButton)sender;
            _prio = check.Name;

        }

        private void _openDia(string header, List<string> zeilen)
        {
            diaHeader.Content = header;
            foreach(var l in zeilen) diaText.Inlines.Add(l + "\r\n");
            dia.IsOpen = true;
        }

        private void dieClose_Click(object sender, RoutedEventArgs e)
        {
            dia.IsOpen = false;
            diaText.Text = "";
        }

        private void _getStyles(string content)
        {
            using (StringReader assFile = new StringReader(content))
            {
                string line = "";

                while ((line = assFile.ReadLine()) != null)
                {
                    if (line.StartsWith("Style:"))
                    {

                        string[] splitLine = line.Split(",", 23, StringSplitOptions.None);
                        string style = splitLine[0].Replace("Style: ", "");

                        if (!_styles.Any(item => item.name == style))
                        {
                            _countStyles++;
                            _styles.Add(new styleList() { id = _countStyles, name = style });
                        }
                    }
                }

            }
        }

        private string _genSubName(string name, string num, bool original = false)
        {
            name = name.Replace(".ass", "");
            if (!_keepOroginalNames && !original) name = _fileName.ToString().Replace("{original}", name).Replace("{num}", num);

            return name;
        }

        private void fileSelector_Click(object sender, RoutedEventArgs e)
        {
            progressbar(true);
            _resetData();
            OpenFileDialog fileDi = new OpenFileDialog();
            fileDi.Multiselect = true;
            fileDi.Filter = "Субтитры (*.ass)|*.ass|Все файлы (*.*)|*.*";
            if (fileDi.ShowDialog() == true)
            {
                _started = true;
                _importPath = fileDi.FileName.Replace(fileDi.SafeFileName, "").ToString();
                _exportPath = _importPath;
                importFile.Text = _importPath;

                foreach (string subtitle in fileDi.SafeFileNames)
                {

                    _countFiles++;
                    string[] paths = { _importPath, subtitle };
                    string path = System.IO.Path.Combine(paths);

                    SubClass sub = new SubClass();
                    sub.epNumber = _aufrunden(_startNum);
                    sub.path = path;
                    sub.name = _genSubName(subtitle, sub.epNumber);
                    sub.originalName = _genSubName(subtitle, sub.epNumber, true);
                    sub.content = File.ReadAllText(path, Encoding.Default);
                    sub.content = Regex.Replace(sub.content, @"\{[^}]*\}", "");
                    sub.lines = _getLines(sub.content);
                    sub.linesCount = _thisLines;
                    _subtitles.Add(sub);
                    _getStyles(sub.content);

                }
                _thisLines = 0;
                _setStatus("Импортированно " + _countFiles.ToString() + " субтитров");
            }

            if(_started)
            {

                exportFile.Text = _exportPath;
                actorsCount.Text = _actors.Count().ToString();
                stylesCount.Text = _styles.Count().ToString();
                linesCount.Text = _countLines.ToString();
                filesCount.Text = _countFiles.ToString();
                infoBox.Visibility = Visibility.Visible;
                exportBox.Visibility = Visibility.Visible;
                exportFiles.IsEnabled = true;
                actorsListBox.ItemsSource = _actors;
                actorsIgnoreListBox.ItemsSource = _actors;
                stylesSelectListBox.ItemsSource = _styles;
                stylesIgnoreSelectListBox.ItemsSource = _styles;
                warning.Visibility = Visibility.Collapsed;

                if (_actors.Count() > 0)
                {
                    actorsStackBox.Visibility = Visibility.Visible;
                    actorsPhraseStack.Visibility = Visibility.Visible;
                    actorsAddStack.Visibility = Visibility.Visible;
                    actorsIgnoreAddStack.Visibility = Visibility.Visible;
                }

                if(_styles.Count() > 0)
                {
                    stylesStackBox.Visibility = Visibility.Visible;
                    stylesSelectStack.Visibility = Visibility.Visible;
                    stylesIgnoreStack.Visibility = Visibility.Visible;
                }

                if(_countFiles > 0)
                {
                    filesCountGroup.Visibility = Visibility.Visible;
                }

                if (_styles.Count() > 0 && _actors.Count() > 0) prioStack.Visibility = Visibility.Visible;
            }
            progressbar();
        }

        private void keepOriginalName_Checked(object sender, RoutedEventArgs e)
        {
            CheckBox chBox = (CheckBox)sender;
            if ((bool)chBox.IsChecked)
            {
                _keepOroginalNames = true;
                if (fileName != null) fileName.IsEnabled = false;
                if (sampleNumDock != null) sampleNumDock.Visibility = Visibility.Collapsed;
                if (startCount != null) startCount.Visibility = Visibility.Collapsed;
            }
            else
            {
                _keepOroginalNames = false;
                if (fileName != null) fileName.IsEnabled = true;
                if (sampleNumDock != null) sampleNumDock.Visibility = Visibility.Visible;
                if (startCount != null) startCount.Visibility = Visibility.Visible;
            }

        }

        private void progressbar(bool status = false, int procent = 0)
        {
            if(progress != null)
            {
                if (status) progress.IsIndeterminate = true;
                else progress.IsIndeterminate = false;

                if (procent > 0) progress.Value = procent;
            }
        }

        private void fileExporter_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new CommonOpenFileDialog();
            dialog.IsFolderPicker = true;
            CommonFileDialogResult result = dialog.ShowDialog();
            _exportPath = dialog.FileName;
            exportFile.Text = _exportPath;
        }

        private void _resetData()
        {
            fileName.Text = "...";
            exportFile.Text = "...";
            infoBox.Visibility = Visibility.Collapsed;
            exportBox.Visibility = Visibility.Collapsed;
            exportFiles.IsEnabled = false;
            actorsStackBox.Visibility = Visibility.Collapsed;
            actorsPhraseStack.Visibility = Visibility.Collapsed;
            actorsAddStack.Visibility = Visibility.Collapsed;
            actorsIgnoreAddStack.Visibility = Visibility.Collapsed;
            stylesStackBox.Visibility = Visibility.Collapsed;
            stylesSelectStack.Visibility = Visibility.Collapsed;
            stylesIgnoreStack.Visibility = Visibility.Collapsed;
            sampleNumDock.Visibility = Visibility.Visible;
            startCount.Visibility = Visibility.Collapsed;
            filesCountGroup.Visibility = Visibility.Collapsed;
            prioStack.Visibility = Visibility.Collapsed;
            _started = false;
            _keepOroginalNames = true;
            _addActorPhrase = false;
            _addActorFile = false;
            _addActorIgnoreFile = false;
            _addIgnoredStylesFile = false;
            _addSelectedStylesFile = false;

            startNum.Text = "1";
            nullCount.Text = "1";
            keepOriginalName.IsChecked = true;
            sampleNum.Text = "This.AweSome.Subs.1.srt";
            linesCount.Text = "0";
            filesCount.Text = "0";
            stylesCount.Text = "0";
            actorsCount.Text = "0";
            _countFiles = 0;
            _countLines = 0;
            _countStyles = 0;
            _nullCount = 1;
            _startNum = 1;

            actorsListBox.SelectedIndex = -1;
            actorsIgnoreListBox.SelectedIndex = -1;
            stylesSelectListBox.SelectedIndex = -1;
            stylesIgnoreSelectListBox.SelectedIndex = -1;

            actorAddPhrase.IsChecked = false;
            actorAddTitle.IsChecked = false;
            actorIgnoreAddTitle.IsChecked = false;
            styleSelectAddTitle.IsChecked = false;
            styleIgnoreAddTitle.IsChecked = false;

            _prio = "actor";

            _setStatus("Настройки сброшены");
        }

        private void exportFiles_ClickAsync(object sender, RoutedEventArgs e)
        {
            int file = 0;
            foreach(var sub in _subtitles)
            {
                file++;
                _setStatus("Экспортируем файл " + file.ToString() + " из " + _countFiles + "...");
                int procent = Convert.ToInt32((file * 100) / _countFiles);
                progressbar(false, procent);

                StringBuilder subExport = new StringBuilder();
                string name = sub.name;
                List<string> nameSuf = new List<string>();
                string acSel = (actorsListBox.SelectedValue != null ? actorsListBox.SelectedValue.ToString() : "");
                string acIgn = (actorsIgnoreListBox.SelectedValue != null ? actorsIgnoreListBox.SelectedValue.ToString() : "");
                string stSel = (stylesSelectListBox.SelectedValue != null ? stylesSelectListBox.SelectedValue.ToString() : "");
                string stIgn = (stylesIgnoreSelectListBox.SelectedValue != null ? stylesIgnoreSelectListBox.SelectedValue.ToString() : "");

                if (_actors.Count() > 0)
                {
                    if(_addActorFile)
                    {
                        if (!_addActorIgnoreFile) nameSuf.Add("[" + acSel + "]");
                    }
                    if (_addActorIgnoreFile) nameSuf.Add("[!" + acIgn + "]");
                }

                if(_styles.Count() > 0)
                {
                    if (_addSelectedStylesFile)
                    {
                        if (!_addIgnoredStylesFile) nameSuf.Add("[" + stSel + "]");
                    }
                    if (_addIgnoredStylesFile) nameSuf.Add("[!" + stIgn + "]");
                }

                if (nameSuf.Count() > 0) name += "_";
                foreach (string n in nameSuf) name += n;
                name += ".srt";

                string[] paths = { _exportPath, name };
                string path = System.IO.Path.Combine(paths);

                foreach(Lines line in sub.lines)
                {
                    bool exportLine = false;

                    if (_addActorFile || _addActorIgnoreFile || _addIgnoredStylesFile || _addSelectedStylesFile)
                    {
                        if (_addActorFile && !_addActorIgnoreFile)
                        {
                            if (line.actor == acSel)
                            {
                                if((_addIgnoredStylesFile && _prio == "actor") || !_addIgnoredStylesFile)
                                    exportLine = true;
                            }
                        }
                        if (_addActorIgnoreFile)
                        {
                            if (line.actor != acSel) exportLine = true;
                        }
                        if (_addSelectedStylesFile && !_addIgnoredStylesFile)
                        {
                            if (line.style == stSel)
                            {
                                if ((_addActorIgnoreFile && _prio == "style") || !_addActorIgnoreFile) exportLine = true;
                            }
                        }
                        if (_addIgnoredStylesFile)
                        {
                            if (line.style != stSel) exportLine = true;
                        }

                    }
                    else
                        exportLine = true;

                    if(exportLine) subExport.Append(line.getLine(_addActorPhrase));
                }

                File.WriteAllText(path, subExport.ToString());

            }

            _setStatus("Экспортировано " + file.ToString() + " файл/а/ов!");
        }

        private void aboutAuthor_Click(object sender, RoutedEventArgs e)
        {
            List<string> author = new List<string>()
            {
                "Немного об идеи самой программы:",
                "Я занимаюсь озвучкой с 16-ти лет. Точнее, занимался. Недавно, решил вспомнить прошлое и наткнулся на",
                "замечательную программу Reaper, которая подхватывает субтитры в формате SRT. А поскольку основной формат,",
                "что я использовал ранее является ASS, то нужен конвертер. Таких, к счастью много, НО... Мне нужен был такой",
                "конвертер, который мог вывести определённые фразы, которые я должен записать, чтобы не просматривать серию целиком",
                "и сэкономить время. Редактор субтиров Aegisub и сам формат ASS поддерживают подобную затею. Достаточно указать актёра",
                "в нужном поле - проблема решится сама собой. Вот, только эти назначения видны лишь в редакторе, а не в субтитрах.",
                "",
                "Ко всему прочему, в рабочей сфере (точее на работе) нужно было подтянуть язык программирования C#. Являясь в основном",
                "разработчиком python и JS, мне понадобилось время освоиться в этой среде.",
                "",
                "Надеюсь, программа окажется полезной!",
                "",
                "Об авторе:",
                "Меня зовут Максим и у меня есть свой форум по разработкам: devcraft.club. Я являюсь разработчиком приложений",
                "на языках C#, PHP и python. В основном я занимаюсь вебразработкой ERM систем."
            };
            _openDia("Об авторе", author);
        }

        private void actorAddPhrase_Checked(object sender, RoutedEventArgs e)
        {
            CheckBox chBox = (CheckBox)sender;
            if ((bool)chBox.IsChecked)
                _addActorPhrase = true;
            else
                _addActorPhrase = false;
        }

        private void actorIgnoreAddTitle_Checked(object sender, RoutedEventArgs e)
        {
            CheckBox chBox = (CheckBox)sender;
            if ((bool)chBox.IsChecked)
                _addActorIgnoreFile = true;
            else
                _addActorIgnoreFile = false;
        }

        private void actorAddTitle_Checked(object sender, RoutedEventArgs e)
        {
            CheckBox chBox = (CheckBox)sender;
            if ((bool)chBox.IsChecked)
                _addActorFile = true;
            else
                _addActorFile = false;
        }

        private void styleSelectAddTitle_Checked(object sender, RoutedEventArgs e)
        {
            CheckBox chBox = (CheckBox)sender;
            if ((bool)chBox.IsChecked)
                _addSelectedStylesFile = true;
            else
                _addSelectedStylesFile = false;
        }

        private void styleIgnoreAddTitle_Checked(object sender, RoutedEventArgs e)
        {
            CheckBox chBox = (CheckBox)sender;
            if ((bool)chBox.IsChecked)
                _addIgnoredStylesFile = true;
            else
                _addIgnoredStylesFile = false;
        }

        private void fileName_TextChanged(object sender, TextChangedEventArgs e)
        {
            string[] notAllowed = { ".", "..", "...", " ", "" };
            if(!notAllowed.Any(na => na == fileName.Text)) _fileName = fileName.Text;
        }

        private void listAllFiles(object sender, RoutedEventArgs e)
        {
            List<string> allFiles = new List<string>();
            foreach (SubClass s in _subtitles) allFiles.Add(s.path);
            _openDia("Перечень субтитров", allFiles);
        }


    }
}
