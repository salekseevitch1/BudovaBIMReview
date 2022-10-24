using Autodesk.Revit.UI;
using System;
using System.Windows;

namespace BudovaBIM.Forms
{
    public partial class ProgressBar : Window, IDisposable
    {
        public bool IsClosed { get; private set; }
        public string PgBarTitle { get; set; }
        public bool IsCanceled { get; set; } = false;

        public ProgressBar(UIApplication application, string title = "", double maximum = 100.0)
        {
            InitializeSize(application);
            InitializeComponent();
            this.progressBar.Maximum = maximum;
            PgBarTitle = title;
            progressBarTitle.Text = GetTitle();
            this.Closed += (s, e) =>
            {
                IsClosed = true;
            };
        }

        private void InitializeSize(UIApplication application)
        {
            this.Owner = System.Windows.Interop.HwndSource.FromHwnd(application.MainWindowHandle).RootVisual as Window;
            this.Top = application.MainWindowExtents.Top;
            this.Left = application.MainWindowExtents.Left;
            this.Width = application.MainWindowExtents.Right - Left;
        }

        public bool Update(double value = 1.0)
        {
            DoEvents();
            progressBar.Value += value;
            progressBarTitle.Text = GetTitle();
            return IsClosed;
        }

        private string GetTitle()
        {
            return $"{PgBarTitle} {progressBar.Value} из {progressBar.Maximum}";
        }

        private void DoEvents()
        {
            System.Windows.Forms.Application.DoEvents();
            System.Windows.Forms.Cursor.Current = System.Windows.Forms.Cursors.WaitCursor;
        }

        public void Dispose()
        {
            if (!IsClosed) Close();
        }

        private void CancelClick(object sender, RoutedEventArgs e)
        {
            IsCanceled = true;
            Close();
        }
    }
}
