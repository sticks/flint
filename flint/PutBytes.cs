using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Text;

namespace flint
{
    public partial class Pebble
    {
        public enum PutBytesType : byte
        {
            Firmware = 1,
            Recovery = 2,
            SystemResources = 3,
            Resources = 4,
            Binary = 5
        }

        enum PutBytesState
        {
            NotStarted,
            WaitForToken,
            InProgress,
            Commit,
            Complete,
            Failed
        }

        class PutBytesClient
        {
            private uint index_;
            private Pebble pebble_;
            private PutBytesType transferType_;
            private byte[] buffer_;
            private PutBytesState state_;
            private uint token_;
            private int left_;
            public bool HasError { get; private set; }

            public PutBytesClient(Pebble pebble, uint index, PutBytesType transferType, byte[] buffer)
            {
                pebble_ = pebble;
                index_ = index;
                transferType_ = transferType;
                buffer_ = buffer;
                state_ = PutBytesState.NotStarted;
                token_ = 0;
                left_ = 0;
            }
            void PutBytesReceived(object sender, MessageReceivedEventArgs e)
            {
                switch (state_)
                {
                    case PutBytesState.WaitForToken:
                        {
                            var unpacked = Util.Unpack("!bI", e.Payload);
                            byte res = Convert.ToByte(unpacked[0]);
                            if (res != 1)
                            {
                                //throw new Exception("failed to wait");
                                HasError = true;
                            }
                            token_ = (uint)unpacked[1];
                            left_ = buffer_.Length;
                            state_ = PutBytesState.InProgress;
                            send();
                        }
                        break;
                    case PutBytesState.InProgress:
                        {
                            var unpacked = Util.Unpack("!b", e.Payload);
                            byte res = Convert.ToByte(unpacked[0]);
                            if (res != 1)
                            {
                                abort();
                                return;
                            }
                            if (left_ > 0)
                                send();
                            else
                            {
                                state_ = PutBytesState.Commit;
                                commit();
                            }
                        }
                        break;
                    case PutBytesState.Commit:
                        {
                            var unpacked = Util.Unpack("!b", e.Payload);
                            byte res = Convert.ToByte(unpacked[0]);
                            if (res != 1)
                            {
                                abort();
                                return;
                            }
                            state_ = PutBytesState.Complete;
                            complete();
                        }
                        break;
                    case PutBytesState.Complete:
                        {
                            var unpacked = Util.Unpack("!b", e.Payload);
                            byte res = Convert.ToByte(unpacked[0]);
                            if (res != 1)
                            {
                                abort();
                                return;
                            }
                            IsDone = true;
                        }
                        break;

                }
            }

            private void complete()
            {
                var data = Util.Pack("!bI", 5, token_ & 0xFFFFFFFF);
                pebble_.sendMessage(Endpoints.PUT_BYTES, data);
            }

            private void commit()
            {
                var data = Util.Pack("!bII", 3, token_ & 0xFFFFFFFF, Util.CRC32(buffer_));
                pebble_.sendMessage(Endpoints.PUT_BYTES, data);
            }

            private void abort()
            {
                var data = Util.Pack("!bI", 4, token_ & 0xFFFFFFFF);
                pebble_.sendMessage(Endpoints.PUT_BYTES, data);
                HasError = true;
            }
            private void send()
            {
                int datalen = Math.Min(left_, 2000);
                int rg = buffer_.Length - left_;
                var msg = Util.Pack("!BII", 2, token_ & 0xFFFFFFFF, datalen);
                msg = msg.Concat(buffer_.Skip(rg).Take(datalen)).ToArray();
                pebble_.sendMessage(Endpoints.PUT_BYTES, msg);
                left_ -= datalen;
                HasError = false;
                IsDone = false;
            }
            public void init()
            {
                if (state_ != PutBytesState.NotStarted)
                {
                    HasError = true;
                    throw new Exception("Already init()ed");
                }
                byte[] data = Util.Pack("!bIbb", 1, buffer_.Length, transferType_, index_);
                pebble_.RegisterEndpointCallback(Endpoints.PUT_BYTES, PutBytesReceived);
                pebble_.sendMessage(Endpoints.PUT_BYTES, data);
                var wait = new EndpointSync<AppbankInstallMessageEventArgs>(pebble_, Endpoints.PUT_BYTES);
                state_ = PutBytesState.WaitForToken;
            }

            public bool IsDone { get; private set; }
        }
    }
}
