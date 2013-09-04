using i.ten.bew.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace i.ten.bew.Messaging
{
    public sealed class ReplyTask<P> : Task<P>, IReplyInfo 
        where P : class
    {
        private class replyReferenceInfo : IReplyInfo       
        {
            private CancellationTokenSource _cancellationSource;
            private Guid _forMessageId;

            public replyReferenceInfo(Guid forMessageId, CancellationTokenSource cancellationSource)
            {
                _cancellationSource = cancellationSource;
                _forMessageId = forMessageId;
            }

            public ServiceBusMessage ReplyMessage
            {
                get;
                set;
            }

            public Guid ForMessageId
            {
                get
                {
                    return _forMessageId;
                }
            }

            public object Payload
            {
                get;
                set;
            }


            public CancellationTokenSource Cancellation
            {
                get 
                {
                    return _cancellationSource;
                }
            }
        }

        private IReplyInfo _resultStore;

        private ReplyTask(IReplyInfo resultStore) 
            : base( 
                    () => {                            
                            return (P)resultStore.Payload;
                    }
                    , 
                    resultStore.Cancellation.Token
            )
        {
            _resultStore = resultStore;
        }

        internal ReplyTask(Guid forMessageId)
            : this(forMessageId, new CancellationTokenSource())
        {
        }

        internal ReplyTask(Guid forMessageId, CancellationTokenSource _cancellation) 
            : this(new replyReferenceInfo(forMessageId, _cancellation) )
        {
        }

        public ServiceBusMessage ReplyMessage
        {
            get
            {
                return _resultStore.ReplyMessage;
            }
            set
            {
                _resultStore.ReplyMessage = value;
            }
        }

        public P Payload
        {
            get
            {
                return (P)_resultStore.Payload;
            }
            set
            {
                _resultStore.Payload = value;
            }
        }

        object IReplyInfo.Payload
        {
            get
            {
                return _resultStore.Payload;
            }
            set
            {
                _resultStore.Payload = value;
            }
        }


        public CancellationTokenSource Cancellation
        {
            get 
            {
                return _resultStore.Cancellation;
            }
        }


        public Guid ForMessageId
        {
            get
            {
                return _resultStore.ForMessageId;
            }
        }
    }
}
