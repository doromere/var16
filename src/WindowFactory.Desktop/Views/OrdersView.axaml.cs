using Avalonia.Controls;
using WindowFactory.Desktop.ViewModels;

namespace WindowFactory.Desktop.Views;

public partial class OrdersView : UserControl
{
    public OrdersView()
    {
        InitializeComponent();
        Grid.DoubleTapped += (_, _) =>
        {
            if (DataContext is OrdersViewModel vm && vm.OpenOrderCommand.CanExecute(null))
                vm.OpenOrderCommand.Execute(null);
        };
    }
}
