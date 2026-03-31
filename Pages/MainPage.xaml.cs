using APP_GoiXetheoGPS.Models;
using APP_GoiXetheoGPS.PageModels;

namespace APP_GoiXetheoGPS.Pages
{
    public partial class MainPage : ContentPage
    {
        public MainPage(MainPageModel model)
        {
            InitializeComponent();
            BindingContext = model;
        }
    }
}