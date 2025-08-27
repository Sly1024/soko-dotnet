using System;
using System.Collections.Generic;

namespace soko
{
    public class PerformanceCounter
    {
        private readonly Func<double> _valueProvider;
        private readonly double _firstValue;
        private double _lastValue;
        private double _lastElapsedSeconds = 0.0;

        public PerformanceCounter(Func<double> valueProvider)
        {
            _valueProvider = valueProvider;
            _firstValue = _valueProvider();
            _lastValue = _firstValue;
        }

        public double Tick(double elapsedSeconds)
        {
            double currentValue = _valueProvider();

            double deltaValue = currentValue - _lastValue;
            double deltaTime = elapsedSeconds - _lastElapsedSeconds;
            _lastValue = currentValue;
            _lastElapsedSeconds = elapsedSeconds;
            if (deltaTime > 0)
                return deltaValue / deltaTime;
            else
                return 0;
        }

        public double Average => _lastValue / _lastElapsedSeconds;
        public double Current => _lastValue;

        public static readonly Dictionary<string, PerformanceCounter> Counters = [];

        public static void Register(string counterName, Func<double> valueProvider)
        {
            var counter = new PerformanceCounter(valueProvider);
            Counters.Add(counterName, counter);
        }
    }
}