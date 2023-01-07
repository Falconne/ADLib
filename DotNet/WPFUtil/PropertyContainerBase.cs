using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WPFUtil
{
    public abstract class PropertyContainerBase : INotifyPropertyChanged
    {
        #region Property Change Events

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        #endregion
    }
}