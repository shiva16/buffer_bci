using System;
using System.IO;
using System.Net.Sockets;

namespace FieldTrip.Buffer
{
    /// <summary>
    /// An implementation of the Java SocketChannel class.
    /// </summary>
	public class SocketChannel
	{
		internal bool socketReady = false;
	 
		private TcpClient mySocket;
		private NetworkStream theStream;

        /// <summary>
        /// The host to which this instance is connected.
        /// </summary>
        public string Host { get; private set; }

        /// <summary>
        /// The port on which this instance is connected.
        /// </summary>
        public int Port { get; private set; }

        public int Timeout { get; private set; } = 10000;

        /// <summary>
        /// Intializes this instance.
        /// </summary>
        public SocketChannel()
		{
		}

        /// <summary>
        /// Finalizer to correctly close the socket.
        /// </summary>
		~SocketChannel()
		{
            try
            { // don't throw within destructor
				mySocket.Close();
			} catch {
			}
		}

		/// <summary>
		/// Connect to the specified hostname and port.
		/// </summary>
		/// <param name="hostname">Hostname.</param>
		/// <param name="port">Port.</param>
		public bool Connect(string hostname, int port)
		{
			try {
				Host = hostname;
				Port = port;
				mySocket = new TcpClient(Host, Port);
				mySocket.NoDelay = true;
				mySocket.ReceiveTimeout = 0; // allow infinite read time
				theStream = mySocket.GetStream();
				socketReady = true;
				// allow infinite read time, Necessary for long wait_dat calls....
				theStream.ReadTimeout = 1000000;//System.Threading.Infinite; 
			} catch (Exception e) {
				socketReady = false;
				throw new IOException("Socket error: " + e);
			}
			return socketReady;
		}

		/// <summary>
		/// Gets the socket.
		/// </summary>
		/// <value>The underlying socket that is connected to the FieldTrip buffer.</value>
		public TcpClient Socket {
			get {
				return mySocket;
			}
		}

		/// <summary>
		/// Write the specified <see cref="FieldTrip.Buffer.ByteBuffer"/> to the FieldTrip buffer. 
		/// </summary>
		/// <param name="src">The buffer to write.</param>
		public int Write(ByteBuffer src)
		{
			int toWrite = (int)src.Remaining;
			byte[] message = new byte[toWrite];
	        
			src.Get(ref message);
	        
			theStream.Write(message, 0, toWrite);
	        
			return toWrite;
		}

		/// <summary>
		/// Read data to the given <see cref="FieldTrip.Buffer.ByteBuffer"/>
		/// </summary>
		/// <param name="dst">The buffer to read to.</param>
		public int Read(ByteBuffer dst)
		{
			int toRead = dst.Capacity;
			byte[] message = new byte[toRead];
	 		
			int readBytes = 0;
			//while(readBytes<toRead){ // this loop is uncessary -- we return number bytes read anyway...
			readBytes += theStream.Read(message, readBytes, toRead - readBytes);
			//}
			dst.Put(message);
			return readBytes;
		}

		/// <summary>
		/// Close this instance.
		/// </summary>
		public void Close()
		{
			if (!socketReady)
				return;
			mySocket.Close();
			socketReady = false;
		}

		/// <summary>
		/// Gets a value indicating whether this instance is connected.
		/// </summary>
		/// <value><c>true</c> if this instance is connected; otherwise, <c>false</c>.</value>
		public bool IsConnected {
			get {
				if (mySocket != null)
					return mySocket.Connected;
				else
					return false;
			}
		}
	}
}
