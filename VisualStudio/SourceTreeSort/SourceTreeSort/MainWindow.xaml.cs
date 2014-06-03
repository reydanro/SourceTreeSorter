using Microsoft.Win32;
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
using System.Xml;

namespace SourceTreeSort
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void buttonAutoScan_Click(object sender, RoutedEventArgs e)
        {
            string path = Autoscan();
            if (path != null)
                textboxBookmarksFile.Text = path;
            else
                MessageBox.Show("Could not auto find the bookmarks xml. You will have to manually browse to it");
        }

        private string Autoscan()
        {
            String[] paths = new String[] { Environment.ExpandEnvironmentVariables("%LocalAppData%"), 
                                            "Atlassian",
                                            "SourceTree",
                                            "bookmarks.xml"};

            String path = System.IO.Path.Combine(paths);

            if (File.Exists(path))
            {
                return path;
            }
            else
            {
                return null;
            }
        }

        private void buttonBrowseXml_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.InitialDirectory = Environment.ExpandEnvironmentVariables("%LocalAppData%");
            dialog.Filter = "XML Files (*.xml)|*.xml";
            dialog.Multiselect = false;

            Nullable<bool> result = dialog.ShowDialog();
            if (result == true)
            {
                textboxBookmarksFile.Text = dialog.FileName as string;
            }
        }

        private void buttonStart_Click(object sender, RoutedEventArgs e)
        {
            Process current = Process.GetCurrentProcess();
            // First, check to see if there are any SourceTree processes open
            foreach (Process p in Process.GetProcesses())
            {
                if (p.Id == current.Id)
                    continue;

                if (p.ProcessName.IndexOf("SourceTree", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    MessageBox.Show("Please close all SourceTree instances in order to sort the bookmarks","SourceTree is currently running");
                    return;
                }
            }

            String fpath = textboxBookmarksFile.Text;

            if (!File.Exists(fpath))
            {
                MessageBox.Show("Could not find target file");
                return;
            }

            // If we reached this point, then we can open the xml and sort it
            try
            {             
                if (sortparamBackupOriginal.IsChecked.Value)
                {
                    File.Copy(fpath, "backup_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + "_" + System.IO.Path.GetFileName(fpath));
                }
                SortBookmarksDocument(fpath);

                MessageBox.Show("Sorted successfully. Please start SourceTree.");
            }catch (Exception ex)
            {
                MessageBox.Show("There was a problem sorting the bookmarks file");
                return;
            }
        }

        private void SortBookmarksDocument(String fpath)
        {
            XmlDocument xmldoc = new XmlDocument();
            xmldoc.Load(fpath);

            // Remove the declaration
            XmlNode xmlDeclaration = null;
            foreach (XmlNode node in xmldoc)
            {
                if (node.NodeType == XmlNodeType.XmlDeclaration)
                {
                    xmlDeclaration = node;
                    xmldoc.RemoveChild(node);
                }
            }
            
            SortNode(xmldoc.SelectSingleNode("ArrayOfTreeViewNode") as XmlElement);

            // Re-insert the declaration
            if (xmlDeclaration != null)
                xmldoc.InsertBefore(xmlDeclaration, xmldoc.DocumentElement);

            xmldoc.Save(fpath);
        }


        private void SortNode(XmlElement root)
        {
            if (root == null)
                return;

            var sortedElements = new List<XmlElement>(root.SelectNodes("TreeViewNode").OfType<XmlElement>());
            sortedElements.Sort(new TreeViewNodeComparer(sortparamFoldersOnTop.IsChecked.Value));

            // Remove all nodes and append the new ones
            foreach (XmlElement elem in sortedElements)
                root.RemoveChild(elem);

            foreach (XmlElement elem in sortedElements)
                root.AppendChild(elem);

            // Recursive sort each of the sub folders
            foreach (XmlElement elem in sortedElements)
            {
                if (elem.GetAttribute("xsi:type") == "BookmarkFolderNode")
                    SortNode(elem.SelectSingleNode("Children") as XmlElement);
            }
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            buttonAutoScan_Click(null, null);
        }
    }

    class TreeViewNodeComparer : IComparer<XmlElement>
    {
        private bool paramFoldersOnTop = true;

        public TreeViewNodeComparer(bool foldersOnTop)
        {
            paramFoldersOnTop = foldersOnTop;
        }

        public int Compare(XmlElement elem1, XmlElement elem2)
        {
            bool elem1Folder = elem1.GetAttribute("xsi:type").ToString().CompareTo("BookmarkFolderNode") == 0;
            bool elem2Folder = elem2.GetAttribute("xsi:type").ToString().CompareTo("BookmarkFolderNode") == 0;

            if (paramFoldersOnTop && elem1Folder != elem2Folder)
            {
                return elem1Folder ? -1 : 1;
            }
            else
            {
                // Get their name
                string elem1Name = (elem1.SelectNodes("Name")[0] as XmlElement).InnerText;
                string elem2Name = (elem2.SelectNodes("Name")[0] as XmlElement).InnerText;

                return elem1Name.CompareTo(elem2Name);
            }
        }

    }
}
