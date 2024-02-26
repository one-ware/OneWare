using CommunityToolkit.Mvvm.ComponentModel;
using Prism.Ioc;

using OneWare.Essentials.Services;

namespace OneWare.Core.ViewModels.Windows
{
    public class WaitCalculatorWindowViewModel : ObservableObject
    {
        private string _frequency = "";
        private int _frequencyUnit = 2;

        private string _result = "";

        private string _wait = "";

        private int _waitUnit = 3;

        //0 = Hz, 1 = KHz, 2 = MHz, 3 = GHz
        public int FrequencyUnit
        {
            get => _frequencyUnit;
            set
            {
                SetProperty(ref _frequencyUnit, value);
                CalculateResult();
            }
        }

        //0 = ps, 1 = ns, 2 = µs, 3 = ms, 4 = s
        public int WaitUnit
        {
            get => _waitUnit;
            set
            {
                SetProperty(ref _waitUnit, value);
                CalculateResult();
            }
        }

        public string Frequency
        {
            get => _frequency;
            set
            {
                SetProperty(ref _frequency, value);
                CalculateResult();
            }
        }

        public string Wait
        {
            get => _wait;
            set
            {
                SetProperty(ref _wait, value);
                CalculateResult();
            }
        }

        public string Result
        {
            get => _result;
            set => SetProperty(ref _result, value);
        }

        public void CalculateResult()
        {
            ContainerLocator.Container.Resolve<ILogger>()?.Log(FrequencyUnit + " " + WaitUnit);
            double.TryParse(Frequency, out var frequency);
            if (FrequencyUnit > -1 && frequency > -1) frequency *= Math.Pow(10, FrequencyUnit * 3);
            double.TryParse(Wait, out var wait);
            if (WaitUnit > -1 && wait > -1) wait /= Math.Pow(10, Math.Abs(4 - WaitUnit) * 3);
            if (wait > -1 && frequency > -1)
            {
                var result = frequency * wait;
                Result = Math.Round(result).ToString();
            }
        }
    }
}