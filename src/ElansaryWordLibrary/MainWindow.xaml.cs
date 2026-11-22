using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ElansaryWordLibrary.Models;
using ElansaryWordLibrary.Services;
using Microsoft.Win32;

namespace ElansaryWordLibrary;

public partial class MainWindow : Window
{
    private readonly DocxSearchService _service = new();
    private List<SearchResult> _results = new();

    public MainWindow()
    {
        InitializeComponent();
    }

    private void AddFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog();
        if (dialog.ShowDialog(this) == true)
        {
            _service.AddFolder(dialog.FolderName);
            MessageBox.Show("تمت إضافة المجلد.");
        }
    }

    private void AddFiles_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Word Documents (*.docx)|*.docx",
            Multiselect = true
        };

        if (dialog.ShowDialog(this) == true)
        {
            _service.AddFiles(dialog.FileNames);
            MessageBox.Show("تمت إضافة الملفات.");
        }
    }

    private void ClearLibrary_Click(object sender, RoutedEventArgs e)
    {
        _service.Clear();
        _results.Clear();
        ResultsTree.Items.Clear();
        DocumentViewer.Clear();
    }

    private void Search_Click(object sender, RoutedEventArgs e) => RunSearch();

    private void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) RunSearch();
    }

    private void RunSearch()
    {
        var query = SearchBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(query)) return;

        _results = _service.Search(query);
        ResultsTree.Items.Clear();

        foreach (var group in _results.GroupBy(r => r.FilePath))
        {
            var fileItem = new TreeViewItem { Header = Path.GetFileName(group.Key), Tag = group.Key };
            foreach (var result in group)
            {
                var item = new TreeViewItem
                {
                    Header = $"فقرة {result.ParagraphIndex + 1}: {result.Snippet}",
                    Tag = result
                };
                fileItem.Items.Add(item);
            }
            ResultsTree.Items.Add(fileItem);
        }
    }

    private void ResultsTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is TreeViewItem item && item.Tag is SearchResult result)
        {
            DocumentViewer.Text = result.FullText;
        }
    }
}
