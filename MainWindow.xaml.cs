using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Xml;

namespace Maintenance
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const int CHECK_ALL = 1000;
        private const int UNCHECK_ALL = 1001;
        private const int RUN_TASKS = 1002;

        private String m_script_file = "tasks.xml";
        private List<String> m_commands = new List<string>();
        private List<CheckBox> m_checkboxes = new List<CheckBox>();
        private List<TaskDefinition> m_pending_tasks = new List<TaskDefinition>();
        private TaskDefinition m_active_task = null;
        private DispatcherTimer m_timer;
        private DateTime m_status_reset_time = DateTime.Now.AddYears(10);

        public MainWindow()
        {
            InitializeComponent();

            int i = 0;
            string[] args = Environment.GetCommandLineArgs();
            while (i < args.Length)
            {
                String arg = args[i++];
                if (arg == "-script")
                {
                    if (i < args.Length) m_script_file = args[i++];
                }
            }

            if (File.Exists(m_script_file))
            {
                try
                {
                    LoadScript(m_script_file);
                }
                catch (Exception ex)
                {
                    AddLabel("Error in script file: " + ex.Message, 10, 10);
                }
            }
            else AddLabel(String.Format("The file {0} does not exist.  Use the -script command line argument to specify a script file.", m_script_file), 10, 10);

            m_timer = new DispatcherTimer();
            m_timer.Tick += new EventHandler(OnTimer);
            m_timer.Interval = new TimeSpan(0, 0, 0, 0, 100);
            m_timer.Start();
        }

        private void LoadScript(String script_file)
        {
            XmlDocument doc = new XmlDocument();
            doc.Load(script_file);
            int tasknum = 0;
            XmlElement root = doc.DocumentElement;
            int w = GetIntAttribute(root, "width", 0);
            int h = GetIntAttribute(root, "height", 0);
            String title = root.GetAttribute("title");
            if (w > 0) this.Width = w;
            if (h > 0) this.Height = h;
            if (title.Length > 0) this.Title = title;
            m_item_container.Children.Clear();
            foreach (XmlNode node in root.ChildNodes)
            {
                XmlElement child = node as XmlElement;
                if (child != null)
                {
                    if (child.Name == "instructions")
                    {
                        double margin_top = GetDoubleAttribute(child, "margin-top", 10.0);
                        double margin_bottom = GetDoubleAttribute(child, "margin-bottom", 10.0);
                        String text = child.InnerText.Trim();
                        AddLabel(text, margin_top, margin_bottom);
                    }
                    if (child.Name == "task")
                    {
                        tasknum += 1;
                        String name = GetChildText(child, "name");
                        String command = GetChildText(child, "command");
                        String arguments = GetChildText(child, "args");
                        bool use_shell = (child.GetAttribute("use-shell").ToLower() == "true");
                        String description = GetChildText(child, "description");
                        TaskDefinition task = new TaskDefinition
                        {
                            Name = name,
                            Description = description,
                            Command = command,
                            Arguments = arguments,
                            UseCommandShell = use_shell
                        };
                        AddManagementItem(task);
                    }
                    if (child.Name == "doscmd")
                    {
                        tasknum += 1;
                        String name = GetChildText(child, "name");
                        String command = "cmd.exe";
                        String args = GetChildText(child, "command");
                        if (child.GetAttribute("keep-open") == "true") args = "/K " + args;
                        else args = "/C " + args;
                        String description = GetChildText(child, "description");
                        TaskDefinition task = new TaskDefinition
                        {
                            Name = name,
                            Description = description,
                            Command = command,
                            Arguments = args,
                            UseCommandShell = true
                        };
                        AddManagementItem(task);
                    }
                    if (child.Name == "panel")
                    {
                        double margin_top = GetDoubleAttribute(child, "margin-top", 10);
                        double margin_bottom = GetDoubleAttribute(child, "margin-bottom", 10);
                        Grid panel = new Grid();
                        //panel.Orientation = Orientation.Horizontal;
                        panel.Margin = new Thickness(10, margin_top, 10, margin_bottom);
                        foreach (var n in child.ChildNodes)
                        {
                            XmlElement el = n as XmlElement;
                            if (el != null)
                            {
                                if (el.Name == "img")
                                {
                                    double mt = GetDoubleAttribute(el, "margin-top", 0);
                                    double ml = GetDoubleAttribute(el, "margin-bottom", 0);
                                    double iw = GetDoubleAttribute(el, "width", 100);
                                    double ih = GetDoubleAttribute(el, "height", 100);
                                    String align = el.GetAttribute("align").ToLower();
                                    String filename = el.GetAttribute("src");
                                    if (filename != ""  &&  File.Exists(filename))
                                    {
                                        Image img = new Image();
                                        BitmapImage bitmap;
                                        FileStream fileStream = new FileStream(filename, FileMode.Open, FileAccess.Read);
                                        bitmap = new System.Windows.Media.Imaging.BitmapImage();
                                        bitmap.BeginInit();
                                        bitmap.StreamSource = fileStream;
                                        bitmap.EndInit();
                                        img.Source = bitmap;
                                        img.Width = iw;
                                        img.Height = ih;
                                        if (align == "left") img.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
                                        if (align == "right") img.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
                                        panel.Children.Add(img);
                                    }
                                }
                            }
                        }
                        m_item_container.Children.Add(panel);
                    }
                    if (child.Name == "separator")
                    {
                        Separator sep = new Separator();
                        m_item_container.Children.Add(sep);
                    }
                }
            }
            //AddLabel("Select the maintenance tasks that you want to run, and then click the Run button.", 20.0, 20.0);
            //AddManagementItem("Create database", 1, "run create_database.exe");
            //AddManagementItem("Install RAPIDS applications", 2, "run install_rapids.exe");
            //AddManagementItem("Install web application", 3, "run install_sldwebapp");

            AddActionButtons();
        }

        private void AddManagementButton(String label, int tag)
        {
            Button button = new Button();
            button.Content = label;
            button.BorderThickness = new Thickness(0);
            button.Background = Brushes.Transparent;
            button.Margin = new Thickness(10, 5, 5, 5);
            button.HorizontalContentAlignment = System.Windows.HorizontalAlignment.Left;
            button.Tag = tag;
            button.Click += new RoutedEventHandler(ItemClickHandler);
            m_item_container.Children.Add(button);
        }

        private void AddManagementItem(TaskDefinition task)
        {
            CheckBox checkbox = new CheckBox();
            checkbox.Content = task.Name;
            checkbox.IsChecked = false;
            //checkbox.BorderThickness = new Thickness(0);
            checkbox.Background = Brushes.Transparent;
            checkbox.Margin = new Thickness(10, 5, 5, 5);
            checkbox.HorizontalContentAlignment = System.Windows.HorizontalAlignment.Left;
            checkbox.Tag = task;

            ToolTip tooltip = new ToolTip { Content = task.Description };
            checkbox.ToolTip = tooltip;

            m_item_container.Children.Add(checkbox);
            m_checkboxes.Add(checkbox);
        }

        private TextBlock AddLabel(String text, double margin_top, double margin_bottom)
        {
            TextBlock result = new TextBlock();
            result.Text = text;
            result.TextWrapping = TextWrapping.WrapWithOverflow;
            result.Margin = new Thickness(10, margin_top, 10, margin_bottom);
            
            m_item_container.Children.Add(result);
            return result;
        }

        private void AddActionButtons()
        {
            StackPanel panel = new StackPanel();
            panel.Orientation = Orientation.Horizontal;
            panel.Margin = new Thickness(10, 10, 10, 10);
            panel.Children.Add(CreateButton("Check All", CHECK_ALL, 100, 0, 10));
            panel.Children.Add(CreateButton("Uncheck All", UNCHECK_ALL, 100, 10, 10));
            panel.Children.Add(CreateButton("Run Tasks", RUN_TASKS, 100, 10, 10));
            m_item_container.Children.Add(panel);
        }

        private Button CreateButton(String text, int tag, int width, double left_margin, double top_margin)
        {
            Button result = new Button();
            result.Content = text;
            result.Width = width;
            result.Tag = tag;
            result.Margin = new Thickness(left_margin, top_margin, 0, 0);
            result.Click += new RoutedEventHandler(ItemClickHandler);
            return result;
        }

        private XmlElement GetChildElement(XmlElement parent, String tagname)
        {
            XmlElement result = null;
            foreach (XmlNode node in parent.ChildNodes)
            {
                XmlElement elem = node as XmlElement;
                if (elem != null  &&  elem.Name == tagname)
                {
                    result = elem;
                    break;
                }
            }
            return result;
        }

        private String GetChildText(XmlElement parent, String tagname, bool trim = true)
        {
            String result = "";
            XmlElement child = GetChildElement(parent, tagname);
            if (child != null)
            {
                result = child.InnerText;
                if (trim) result = result.Trim();
            }
            return result;
        }

        private int GetIntAttribute(XmlElement elem, String attribute_name, int default_value)
        {
            int result = default_value;
            String strval = elem.GetAttribute(attribute_name);
            if (!String.IsNullOrEmpty(strval))
            {
                if (!Int32.TryParse(strval, out result)) result = default_value;
            }
            return result;
        }

        private double GetDoubleAttribute(XmlElement elem, String attribute_name, double default_value)
        {
            double result = default_value;
            String strval = elem.GetAttribute(attribute_name);
            if (!String.IsNullOrEmpty(strval))
            {
                if (!Double.TryParse(strval, out result)) result = default_value;
            }
            return result;
        }

        private void StartTask(TaskDefinition task)
        {
            m_active_task = task;
            Process p = new Process();
            p.StartInfo.FileName = task.Command;
            //p.StartInfo.Verb = "Print";
            //p.StartInfo.CreateNoWindow = true;
            p.StartInfo.UseShellExecute = task.UseCommandShell;
            p.StartInfo.Arguments = task.Arguments;
            p.EnableRaisingEvents = true;
            p.Exited += new EventHandler(OnProcessExited);
            task.ProcessHandle = p;
            //MessageBox.Show(task.Description);
            try
            {
                SetStatus(2000, "Starting \"{0}\"", task.Command);
                p.Start();
            }
            catch(Exception ex)
            {
                m_active_task = null;
                task.ProcessHandle = null;
                MessageBox.Show(ex.Message);
            }

        }

        private void StartNextTask()
        {
            if (m_pending_tasks.Count > 0)
            {
                TaskDefinition task = m_pending_tasks[0];
                m_pending_tasks.RemoveAt(0);
                StartTask(task);
            }
        }

        private void SetStatus(int duration_ms, String fmt, params object[] args)
        {
            lock(m_status_text)
            {
                String text = String.Format(fmt, args);
                Dispatcher.Invoke((Action)delegate() { m_status_text.Content = text; });
                if (duration_ms > 0) m_status_reset_time = DateTime.Now.AddMilliseconds(duration_ms);
                else m_status_reset_time = DateTime.Now.AddYears(10);
            }
        }

        //#################################################################
        //
        // EVENT HANDLERS
        //
        //#################################################################

        private void ItemClickHandler(Object sender, RoutedEventArgs args)
        {
            Button b = sender as Button;
            if (b != null)
            {
                int id = (int)b.Tag;
                switch (id)
                {
                    case CHECK_ALL:
                        foreach (CheckBox cb in m_checkboxes) cb.IsChecked = true;
                        break;
                    case UNCHECK_ALL:
                        foreach (CheckBox cb in m_checkboxes) cb.IsChecked = false;
                        break;
                    case RUN_TASKS:
                        foreach (CheckBox cb in m_checkboxes)
                        {
                            if (cb.IsChecked == true)
                            {
                                TaskDefinition task = cb.Tag as TaskDefinition;
                                if (task != null)
                                {
                                    m_pending_tasks.Add(task);
                                }
                            }
                        }
                        StartNextTask();
                        break;
                }
            }
        }

        private void OnProcessExited(object sender, System.EventArgs e)
        {

            m_active_task.ProcessHandle = null;
            //MessageBox.Show("Exited: " + m_active_task.Name);
            m_active_task = null;
            if (m_pending_tasks.Count > 0)
            {
                TaskDefinition task = m_pending_tasks[0];
                m_pending_tasks.RemoveAt(0);
                StartTask(task);
            }
        }


        private void OnTimer(object sender, EventArgs e)
        {
            DateTime now = DateTime.Now;
            if (now > m_status_reset_time)
            {
                m_status_reset_time = now.AddYears(10);
                m_status_text.Content = "Ready";
            }
        }

        private void OnExitApp(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }

    class TaskDefinition
    {
        public String Name { get; set; }
        public String Description { get; set; }
        public String Command { get; set; }
        public String Arguments { get; set; }
        public bool UseCommandShell { get; set; }
        public Process ProcessHandle { get; set; }
    }
}
