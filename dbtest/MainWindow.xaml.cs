
using System.Windows;


namespace dbtest
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            var db = DBManager.Instance;
            db.Start();
            db.ReadTest();
        }
    }
}
