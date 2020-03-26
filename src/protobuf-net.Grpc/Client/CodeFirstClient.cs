using Grpc.Core;
namespace ProtoBuf.Grpc.Client
{
    public abstract class CodeFirstClient<TService> : ClientBase where TService : class
    {
        public CodeFirstClient(CallInvoker callInvoker) : base(callInvoker) { }
        public TService Service => (TService)(object)this;

        public static implicit operator TService(CodeFirstClient<TService> client) => client.Service;
    }
}
