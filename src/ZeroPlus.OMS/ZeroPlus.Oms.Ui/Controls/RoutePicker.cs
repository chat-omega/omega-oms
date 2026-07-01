using DevExpress.Xpf.Editors;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;

namespace ZeroPlus.Oms.Ui.Controls
{
    public class RoutePicker : ComboBoxEdit
    {
        private const string DmaListPartName = "PART_DmaRoutes";
        private const string SorListPartName = "PART_SorRoutes";

        private static readonly ThreadLocal<ControlTemplate> DefaultPopupTemplate =
            new ThreadLocal<ControlTemplate>(LoadDefaultPopupTemplate);

        private ListBoxEdit _dmaList;
        private ListBoxEdit _sorList;
        private bool _suppressListSelectionSync;

        public static readonly DependencyProperty DmaRoutesListProperty =
            DependencyProperty.Register(
                nameof(DmaRoutesList),
                typeof(IEnumerable),
                typeof(RoutePicker),
                new PropertyMetadata(null, OnRoutesListsChanged));

        public static readonly DependencyProperty SorRoutesListProperty =
            DependencyProperty.Register(
                nameof(SorRoutesList),
                typeof(IEnumerable),
                typeof(RoutePicker),
                new PropertyMetadata(null, OnRoutesListsChanged));

        public static readonly DependencyProperty RouteChangedCommandProperty =
            DependencyProperty.Register(
                nameof(RouteChangedCommand),
                typeof(ICommand),
                typeof(RoutePicker),
                new PropertyMetadata(null));

        public IEnumerable DmaRoutesList
        {
            get => (IEnumerable)GetValue(DmaRoutesListProperty);
            set => SetValue(DmaRoutesListProperty, value);
        }

        public IEnumerable SorRoutesList
        {
            get => (IEnumerable)GetValue(SorRoutesListProperty);
            set => SetValue(SorRoutesListProperty, value);
        }

        public ICommand RouteChangedCommand
        {
            get => (ICommand)GetValue(RouteChangedCommandProperty);
            set => SetValue(RouteChangedCommandProperty, value);
        }

        public RoutePicker()
        {
            IsTextEditable = false;
            PopupContentTemplate = DefaultPopupTemplate.Value;

            PopupOpened += OnPopupOpened;
            EditValueChanged += OnRouteEditValueChanged;
        }

        private static ControlTemplate LoadDefaultPopupTemplate()
        {
            const string xaml =
                @"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                                  xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
                                  xmlns:dxe='http://schemas.devexpress.com/winfx/2008/xaml/editors'>
                    <Grid Background='#1F1F22' MinWidth='240' MinHeight='160'>
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width='*' />
                            <ColumnDefinition Width='Auto' />
                            <ColumnDefinition Width='*' />
                        </Grid.ColumnDefinitions>
                        <Grid.RowDefinitions>
                            <RowDefinition Height='Auto' />
                            <RowDefinition Height='*' />
                        </Grid.RowDefinitions>
                        <TextBlock Grid.Row='0' Grid.Column='0' Text='DMA (Exch)' Margin='8,4'
                                   Foreground='#9CA3AF' FontWeight='SemiBold' FontSize='11' />
                        <TextBlock Grid.Row='0' Grid.Column='2' Text='SOR (Algo)' Margin='8,4'
                                   Foreground='#9CA3AF' FontWeight='SemiBold' FontSize='11' />
                        <Border Grid.Row='1' Grid.Column='1' Width='1' Background='#2D2D31' Margin='2,0'/>
                        <dxe:ListBoxEdit x:Name='PART_DmaRoutes' Grid.Row='1' Grid.Column='0'
                                         ShowBorder='False' Background='Transparent' />
                        <dxe:ListBoxEdit x:Name='PART_SorRoutes' Grid.Row='1' Grid.Column='2'
                                         ShowBorder='False' Background='Transparent' />
                    </Grid>
                </ControlTemplate>";
            return (ControlTemplate)XamlReader.Parse(xaml);
        }

        private static void OnRoutesListsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is RoutePicker picker)
            {
                picker.RefreshListSources();
            }
        }

        private void OnPopupOpened(object sender, RoutedEventArgs e)
        {
            RebindListParts();
            RefreshListSources();
            SyncListSelection(EditValue as string);
        }

        private void OnRouteEditValueChanged(object sender, EditValueChangedEventArgs e)
        {
            SyncListSelection(e.NewValue as string);

            var cmd = RouteChangedCommand;
            if (cmd != null && cmd.CanExecute(e.NewValue))
            {
                cmd.Execute(e.NewValue);
            }
        }

        private void RebindListParts()
        {
            ListBoxEdit dma = null;
            ListBoxEdit sor = null;

            if (PopupRoot is FrameworkElement popupRoot)
            {
                dma = FindNamedDescendant<ListBoxEdit>(popupRoot, DmaListPartName);
                sor = FindNamedDescendant<ListBoxEdit>(popupRoot, SorListPartName);
            }

            if (!ReferenceEquals(dma, _dmaList))
            {
                if (_dmaList != null)
                {
                    _dmaList.EditValueChanged -= OnListEditValueChanged;
                }
                _dmaList = dma;
                if (_dmaList != null)
                {
                    _dmaList.EditValueChanged += OnListEditValueChanged;
                }
            }

            if (!ReferenceEquals(sor, _sorList))
            {
                if (_sorList != null)
                {
                    _sorList.EditValueChanged -= OnListEditValueChanged;
                }
                _sorList = sor;
                if (_sorList != null)
                {
                    _sorList.EditValueChanged += OnListEditValueChanged;
                }
            }
        }

        private void RefreshListSources()
        {
            if (_dmaList != null)
            {
                _dmaList.ItemsSource = DmaRoutesList;
            }
            if (_sorList != null)
            {
                _sorList.ItemsSource = SorRoutesList;
            }
        }

        private void SyncListSelection(string selectedRoute)
        {
            if (_dmaList == null && _sorList == null)
            {
                return;
            }

            _suppressListSelectionSync = true;
            try
            {
                if (_dmaList != null)
                {
                    _dmaList.SelectedItem = ListContains(DmaRoutesList, selectedRoute) ? selectedRoute : null;
                }
                if (_sorList != null)
                {
                    _sorList.SelectedItem = ListContains(SorRoutesList, selectedRoute) ? selectedRoute : null;
                }
            }
            finally
            {
                _suppressListSelectionSync = false;
            }
        }

        private static bool ListContains(IEnumerable source, string value)
        {
            if (source == null || value == null)
            {
                return false;
            }
            foreach (var item in source)
            {
                if (string.Equals(item as string, value, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private void OnListEditValueChanged(object sender, EditValueChangedEventArgs e)
        {
            if (_suppressListSelectionSync)
            {
                return;
            }
            if (sender is ListBoxEdit list && e.NewValue is string route && !string.IsNullOrEmpty(route))
            {
                _suppressListSelectionSync = true;
                try
                {
                    var other = ReferenceEquals(list, _dmaList) ? _sorList : _dmaList;
                    if (other != null)
                    {
                        other.SelectedItem = null;
                    }
                }
                finally
                {
                    _suppressListSelectionSync = false;
                }

                EditValue = route;
                ClosePopup();
            }
        }

        private static T FindNamedDescendant<T>(DependencyObject root, string name) where T : FrameworkElement
        {
            if (root == null)
            {
                return null;
            }

            var queue = new Queue<DependencyObject>();
            queue.Enqueue(root);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current is T match && match.Name == name)
                {
                    return match;
                }
                int count = VisualTreeHelper.GetChildrenCount(current);
                for (int i = 0; i < count; i++)
                {
                    queue.Enqueue(VisualTreeHelper.GetChild(current, i));
                }
            }
            return null;
        }
    }
}
