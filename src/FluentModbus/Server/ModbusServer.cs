﻿using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

[assembly: InternalsVisibleTo("FluentModbus.Tests")]

namespace FluentModbus
{
    /// <summary>
    /// Base class for a Modbus server.
    /// </summary>
    public abstract class ModbusServer : IDisposable
    {
        #region Events

        /// <summary>
        /// Occurs after one or more registers changed.
        /// </summary>
        public event EventHandler<List<int>> RegistersChanged;

        /// <summary>
        /// Occurs after one or more coils changed.
        /// </summary>
        public event EventHandler<List<int>> CoilsChanged;

        #endregion

        #region Fields

        private Task _task_process_requests;
        private ManualResetEventSlim _manualResetEvent;

        private byte[] _inputRegisterBuffer;
        private byte[] _holdingRegisterBuffer;
        private byte[] _coilBuffer;
        private byte[] _discreteInputBuffer;

        private int _inputRegisterSize;
        private int _holdingRegisterSize;
        private int _coilSize;
        private int _discreteInputSize;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ModbusServer"/>.
        /// </summary>
        /// <param name="isAsynchronous">A boolean which indicates if the server responds to client requests asynchronously (immediately) or synchronously (regularly at fixed events).</param>
        protected ModbusServer(bool isAsynchronous)
        {
            this.Lock = this;
            this.IsAsynchronous = isAsynchronous;

            this.MaxInputRegisterAddress = UInt16.MaxValue;
            this.MaxHoldingRegisterAddress = UInt16.MaxValue;
            this.MaxCoilAddress = UInt16.MaxValue;
            this.MaxDiscreteInputAddress = UInt16.MaxValue;

            _inputRegisterSize = (this.MaxInputRegisterAddress + 1) * 2;
            _inputRegisterBuffer = new byte[_inputRegisterSize];

            _holdingRegisterSize = (this.MaxHoldingRegisterAddress + 1) * 2;
            _holdingRegisterBuffer = new byte[_holdingRegisterSize];

            _coilSize = (this.MaxCoilAddress + 1 + 7) / 8;
            _coilBuffer = new byte[_coilSize];

            _discreteInputSize = (this.MaxDiscreteInputAddress + 1 + 7) / 8;
            _discreteInputBuffer = new byte[_discreteInputSize];

            _manualResetEvent = new ManualResetEventSlim(false);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the lock object. For synchronous operation only.
        /// </summary>
        public object Lock { get; }

        /// <summary>
        /// Gets the operation mode.
        /// </summary>
        public bool IsAsynchronous { get; }

        /// <summary>
        /// Gets the maximum input register address.
        /// </summary>
        public UInt16 MaxInputRegisterAddress { get; }

        /// <summary>
        /// Gets the maximum holding register address.
        /// </summary>
        public UInt16 MaxHoldingRegisterAddress { get; }

        /// <summary>
        /// Gets the maximum coil address.
        /// </summary>
        public UInt16 MaxCoilAddress { get; }

        /// <summary>
        /// Gets the maximum discrete input address.
        /// </summary>
        public UInt16 MaxDiscreteInputAddress { get; }

        /// <summary>
        /// Gets or sets a method that validates each client request.
        /// </summary>
        public Func<ModbusFunctionCode, ushort, ushort, ModbusExceptionCode> RequestValidator { get; set; }

        /// <summary>
        /// Gets or sets whether the events should be raised when register or coil data changes. Default: false.
        /// </summary>
        public bool EnableRaisingEvents { get; set; }

        private protected CancellationTokenSource CTS { get; private set; }

        private protected bool IsReady
        {
            get
            {
                return !_manualResetEvent.Wait(TimeSpan.Zero);
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Gets the input register as <see cref="UInt16"/> array.
        /// </summary>
        public Span<short> GetInputRegisters()
        {
            return MemoryMarshal.Cast<byte, short>(this.GetInputRegisterBuffer());
        }

        /// <summary>
        /// Gets the input register buffer as type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The type of the returned array.</typeparam>
        public Span<T> GetInputRegisterBuffer<T>() where T : unmanaged
        {
            return MemoryMarshal.Cast<byte, T>(this.GetInputRegisterBuffer());
        }

        /// <summary>
        /// Low level API. Use the generic version for easy access. This method gets the input register buffer as byte array.
        /// </summary>
        public Span<byte> GetInputRegisterBuffer()
        {
            return _inputRegisterBuffer;
        }

        /// <summary>
        /// Gets the holding register as <see cref="UInt16"/> array.
        /// </summary>
        public Span<short> GetHoldingRegisters()
        {
            return MemoryMarshal.Cast<byte, short>(this.GetHoldingRegisterBuffer());
        }

        /// <summary>
        /// Gets the holding register buffer as type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The type of the returned array.</typeparam>
        public Span<T> GetHoldingRegisterBuffer<T>() where T : unmanaged
        {
            return MemoryMarshal.Cast<byte, T>(this.GetHoldingRegisterBuffer());
        }

        /// <summary>
        /// Low level API. Use the generic version for easy access. This method gets the holding register buffer as byte array.
        /// </summary>
        public Span<byte> GetHoldingRegisterBuffer()
        {
            return _holdingRegisterBuffer;
        }

        /// <summary>
        /// Gets the coils as <see cref="byte"/> array.
        /// </summary>
        public Span<byte> GetCoils()
        {
            return this.GetCoilBuffer();
        }

        /// <summary>
        /// Gets the coil buffer as type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The type of the returned array.</typeparam>
        public Span<T> GetCoilBuffer<T>() where T : unmanaged
        {
            return MemoryMarshal.Cast<byte, T>(this.GetCoilBuffer());
        }

        /// <summary>
        /// Low level API. Use the generic version for easy access. This method gets the coil buffer as byte array.
        /// </summary>
        public Span<byte> GetCoilBuffer()
        {
            return _coilBuffer;
        }

        /// <summary>
        /// Gets the discrete inputs as <see cref="byte"/> array.
        /// </summary>
        public Span<byte> GetDiscreteInputs()
        {
            return this.GetDiscreteInputBuffer();
        }

        /// <summary>
        /// Gets the discrete input buffer as type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The type of the returned array.</typeparam>
        public Span<T> GetDiscreteInputBuffer<T>() where T : unmanaged
        {
            return MemoryMarshal.Cast<byte, T>(this.GetDiscreteInputBuffer());
        }

        /// <summary>
        /// Low level API. Use the generic version for easy access. This method gets the discrete input buffer as byte array.
        /// </summary>
        public Span<byte> GetDiscreteInputBuffer()
        {
            return _discreteInputBuffer;
        }

        /// <summary>
        /// Clears all buffer contents.
        /// </summary>
        public void ClearBuffers()
        {
            this.GetInputRegisterBuffer().Clear();
            this.GetHoldingRegisterBuffer().Clear();
            this.GetCoilBuffer().Clear();
            this.GetDiscreteInputBuffer().Clear();
        }

        /// <summary>
        /// Serve all available client requests. For synchronous operation only.
        /// </summary>
        public void Update()
        {
            if (this.IsAsynchronous || !this.IsReady)
                return;

            _manualResetEvent.Set();
        }

        /// <summary>
        /// Stops the server operation.
        /// </summary>
        public virtual void Stop()
        {
            this.CTS?.Cancel();
            _manualResetEvent?.Set();

            try
            {
                _task_process_requests?.Wait();
            }
            catch (Exception ex) when (ex.InnerException.GetType() == typeof(TaskCanceledException))
            {
                //
            }
        }

        /// <summary>
        /// Starts the server operation.
        /// </summary>
        protected virtual void Start()
        {
            this.CTS = new CancellationTokenSource();

            if (!this.IsAsynchronous)
            {
                // only process requests when it is explicitly triggered
                _task_process_requests = Task.Run(() =>
                {
                    _manualResetEvent.Wait(this.CTS.Token);

                    while (!this.CTS.IsCancellationRequested)
                    {
                        this.ProcessRequests();

                        _manualResetEvent.Reset();
                        _manualResetEvent.Wait(this.CTS.Token);
                    }
                }, this.CTS.Token);
            }
        }

        /// <summary>
        /// Process incoming requests.
        /// </summary>
        protected abstract void ProcessRequests();

        internal void OnRegistersChanged(List<int> registers)
        {
            this.RegistersChanged?.Invoke(this, registers);
        }

        internal void OnCoilsChanged(List<int> coils)
        {
            this.CoilsChanged?.Invoke(this, coils);
        }

        #endregion

        #region IDisposable Support

        private bool disposedValue = false;

        /// <summary>
        /// Disposes the <see cref="ModbusServer"/> and frees all managed and unmanaged resources.
        /// </summary>
        /// <param name="disposing">A value indicating if the finalizer or the dispose method triggered the dispose process.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                    this.Stop();

                disposedValue = true;
            }
        }

        /// <summary>
        /// Disposes the <see cref="ModbusServer"/> and frees all managed and unmanaged resources.
        /// </summary>
        ~ModbusServer()
        {
            Dispose(false);
        }

        /// <summary>
        /// Disposes the buffers.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
