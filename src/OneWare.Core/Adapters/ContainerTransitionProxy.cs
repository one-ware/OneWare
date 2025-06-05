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

            // Replay all operations on target container
            while (_operationQueue.Count > 0)
            {
                var operation = _operationQueue.Dequeue();
                operation.Execute(_targetContainer);
            }

            _isReleased = true;
        }

        public void Register(Type serviceType, Type implementationType, string name = null, bool isSingleton = false)
        {
            if (_isReleased) throw new InvalidOperationException("Cannot register after release");

            var operation = new RegisterOperation(serviceType, implementationType, name, isSingleton);
            operation.Execute(_initialContainer);
            _operationQueue.Enqueue(operation);
        }

        public void RegisterInstance(Type serviceType, object instance, string name = null)
        {
            if (_isReleased) throw new InvalidOperationException("Cannot register after release");

            var operation = new RegisterInstanceOperation(serviceType, instance, name);
            operation.Execute(_initialContainer);
            _operationQueue.Enqueue(operation);
        }

        public object Resolve(Type serviceType, string name = null)
        {
            return _isReleased
                ? _targetContainer.Resolve(serviceType, name)
                : _initialContainer.Resolve(serviceType, name);
        }

        public bool IsRegistered(Type serviceType, string name = null)
        {
            return _isReleased
                ? _targetContainer.IsRegistered(serviceType, name)
                : _initialContainer.IsRegistered(serviceType, name);
        }

        // Operation pattern to record container operations
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
