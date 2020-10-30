using System.ComponentModel;
using Xamarin.Forms;
using ModulacionDigital.ViewModels;

namespace ModulacionDigital.Views
{
    public partial class ItemDetailPage : ContentPage
    {
        public ItemDetailPage()
        {
            InitializeComponent();
            BindingContext = new ItemDetailViewModel();
        }
    }
}