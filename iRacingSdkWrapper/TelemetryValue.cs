﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using iRSDKSharp;
using iRacingSdkWrapper.Bitfields;

namespace iRacingSdkWrapper
{
    public abstract class TelemetryValue
    {
        protected TelemetryValue(iRSDKSharp.iRacingSDK sdk, string name)
        {
            if (sdk == null) throw new ArgumentNullException("sdk");
            if (!sdk.VarHeaders.ContainsKey(name))
                throw new ArgumentException("No telemetry value with the specified name exists.");


            var header = sdk.VarHeaders[name];
            _Name = name;
            _Description = header.Desc;
            _Unit = header.Unit;
        }

        private readonly string _Name;
        /// <summary>
        /// The name of this telemetry value parameter.
        /// </summary>
        public string Name { get { return _Name; } }

        private readonly string _Description;
        /// <summary>
        /// The description of this parameter.
        /// </summary>
        public string Description { get { return _Description; } }

        private readonly string _Unit;
        /// <summary>
        /// The real world unit for this parameter.
        /// </summary>
        public string Unit { get { return _Unit; } }

        public abstract object GetValue();
    }

    /// <summary>
    /// Represents a telemetry parameter of the specified type.
    /// </summary>
    /// <typeparam name="T">The .NET type of this parameter (int, char, float, double, bool, or arrays)</typeparam>
    public sealed class TelemetryValue<T> : TelemetryValue 
    {
        public TelemetryValue(iRSDKSharp.iRacingSDK sdk, string name)
            : base(sdk, name)
        {
            this.GetData(sdk);
        }

        private void GetData(iRacingSDK sdk)
        {
            var data = sdk.GetData(this.Name);

            if (data != null)
            {
                var type = typeof(T);
                if (type.BaseType != null && type.BaseType.IsGenericType && type.BaseType.GetGenericTypeDefinition() == typeof(BitfieldBase<>))
                {
                    _Value = (T)Activator.CreateInstance(type, new[] { data });
                }
                else
                {
                    _Value = (T)data;
                }
            }
        }

        private T _Value;
        /// <summary>
        /// The value of this parameter.
        /// </summary>
        public T Value { get { return _Value; } }

        public override object GetValue()
        {
            return this.Value;
        }

        public override string ToString()
        {
            return string.Format("{0} {1}", this.Value, this.Unit);
        }
    }
}
