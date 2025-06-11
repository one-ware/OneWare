using System;
using System.Collections.Generic;

namespace OneWare.Core.Adapters
{
    public class ContainerTransitionProxy : IContainerAdapter
    {
        private readonly IContainerAdapter _initialContainer;
        private IContainerAdapter _targetContainer;
        private readonly Queue<ContainerOperation> _operationQueue = new Queue<ContainerOperation>();
        private bool _isReleased = false;

        public ContainerTransitionProxy(IContainerAdapter initialContainer)
        {
            _initialContainer = initialContainer ?? throw new ArgumentNullException(nameof(initialContainer));
        }

        public void SetTargetContainer(IContainerAdapter targetContainer)
        {
            if (_isReleased) throw new InvalidOperationException("Target container already released");
            _targetContainer = targetContainer;
        }

        public void ReleaseToTarget()
        {
            if (_targetContainer == null) throw new InvalidOperationException("Target container not set");
            if (_isReleased) return;

            while (_operationQueue.Count > 0)
            {
                var operation = _operationQueue.Dequeue();
                operation.Execute(_targetContainer);
            }

            _isReleased = true;
        }

        public void Register(Type serviceType, Type implementationType, string name = null, bool isSingleton = false)
        {
            ThrowIfReleased();
            var operation = new RegisterOperation(serviceType, implementationType, name, isSingleton);
            operation.Execute(_initialContainer);
            _operationQueue.Enqueue(operation);
        }

        public void RegisterInstance(Type serviceType, object instance, string name = null)
        {
            ThrowIfReleased();
            var operation = new RegisterInstanceOperation(serviceType, instance, name);
            operation.Execute(_initialContainer);
            _operationQueue.Enqueue(operation);
        }

        public object Resolve(Type serviceType, string name = null)
        {
            return (_isReleased ? _targetContainer : _initialContainer).Resolve(serviceType, name);
        }

        public T Resolve<T>(string name = null)
        {
            return (_isReleased ? _targetContainer : _initialContainer).Resolve<T>(name);
        }

        public void Register<TService, TImplementation>(string name = null, bool isSingleton = false)
            where TImplementation : TService
        {
            Register(typeof(TService), typeof(TImplementation), name, isSingleton);
        }

        public void RegisterInstance<TService>(TService instance, string name = null)
        {
            RegisterInstance(typeof(TService), instance, name);
        }

        public bool IsRegistered(Type serviceType, string name = null)
        {
            return (_isReleased ? _targetContainer : _initialContainer).IsRegistered(serviceType, name);
        }

        public void Build()
        {
            if (_isReleased)
                _targetContainer?.Build();
            else
                _initialContainer.Build();
        }

        private void ThrowIfReleased()
        {
            if (_isReleased)
                throw new InvalidOperationException("Cannot register after release");
        }

        // ---------------- Operation Pattern ----------------

        private abstract class ContainerOperation
        {
            public abstract void Execute(IContainerAdapter container);
        }

        private class RegisterOperation : ContainerOperation
        {
            private readonly Type _serviceType;
            private readonly Type _implementationType;
            private readonly string _name;
            private readonly bool _isSingleton;

            public RegisterOperation(Type serviceType, Type implementationType, string name, bool isSingleton)
            {
                _serviceType = serviceType;
                _implementationType = implementationType;
                _name = name;
                _isSingleton = isSingleton;
            }

            public override void Execute(IContainerAdapter container)
            {
                container.Register(_serviceType, _implementationType, _name, _isSingleton);
            }
        }

        private class RegisterInstanceOperation : ContainerOperation
        {
            private readonly Type _serviceType;
            private readonly object _instance;
            private readonly string _name;

            public RegisterInstanceOperation(Type serviceType, object instance, string name)
            {
                _serviceType = serviceType;
                _instance = instance;
                _name = name;
            }

            public override void Execute(IContainerAdapter container)
            {
                container.RegisterInstance(_serviceType, _instance, _name);
            }
        }
    }
}
