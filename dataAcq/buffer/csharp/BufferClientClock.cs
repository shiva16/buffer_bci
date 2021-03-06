/*
 * Copyright (C) 2013, Jason Farquhar
 *
 * Extension of bufferclient to add ability to fill in event sample number if it is negative
 * based on the output of the system clock and tracking the mapping between clock-time and sample-time
 */
using System;

namespace FieldTrip.Buffer
{
	public class BufferClientClock : BufferClient
	{

		protected ClockSync clockSync = null;
		public long maxSampError = 10000;
		// BODGE: very very very big!
		public long updateInterval = 3000;
		// at least every 3seconds
		public long minUpdateInterval = 10;
		// at least 10ms (100Hz) between clock updates
		protected int numWrong = 0;
		// count of number wrong predictions... if too many then reset the clock

		public BufferClientClock()
			: base()
		{
			clockSync = new ClockSync();
		}

		public BufferClientClock(double alpha)
			: base()
		{
			clockSync = new ClockSync(alpha);
		}

		public BufferClientClock(ByteOrder order)
			: base(order)
		{
			clockSync = new ClockSync();
		}

		public BufferClientClock(ByteOrder order, double alpha)
			: base(order)
		{
			clockSync = new ClockSync(alpha);
		}

		//--------------------------------------------------------------------
		// methods offering additional useful functionality

        /// <summary>
        /// Get the host associated with the FieldTrip buffer.
        /// </summary>
        /// <returns>The host associated with the FieldTrip buffer.</returns>
		public string GetHost()
		{
			return SockChan.Host;
		}

        /// <summary>
        /// Get the port associated with the FieldTrip buffer.
        /// </summary>
        /// <returns>The port associated with the FieldTrip buffer.</returns>
		public int GetPort()
		{
			return SockChan.Port;
		}

		//--------------------------------------------------------------------
		// overridden methods to
		// Fill in the estimated sample info

        /// <summary>
        /// Puts an event to the connected FieldTrip buffer.
        /// </summary>
        /// <param name="e">The event to send.</param>
        /// <returns>The sent buffer event.</returns>
		override public BufferEvent PutEvent(BufferEvent e)
		{
			if (e.Sample < 0) {
				e.Sample = (int)GetSampOrPoll();
			}
			return base.PutEvent(e);
		}

        /// <summary>
        /// Sends an array of events to the connected FieldTrip buffer.
        /// </summary>
        /// <param name="e">The events to send.</param>
		override public void PutEvents(BufferEvent[] e)
		{
			int samp = -1;
			for (int i = 0; i < e.Length; i++) {			 
				if (e[i].Sample < 0) {
					if (samp < 0)
						samp = (int)GetSampOrPoll();
					e[i].Sample = samp;
				}
			}
			base.PutEvents(e);
		}
		// use the returned sample info to update the clock sync
		override public SamplesEventsCount Wait(int nSamples, int nEvents, int timeout)
		{
			//Console.WriteLine("clock update");
			SamplesEventsCount secount = base.Wait(nSamples, nEvents, timeout);
			double deltaSamples = clockSync.GetSamp() - secount.NumSamples; // delta between true and estimated
			//Console.WriteLine("sampErr="+getSampErr() + " d(samp) " + deltaSamples + " sampThresh= " + clockSync.m*1000.0*.5);
			if (GetSampErr() < maxSampError) {
				if (deltaSamples > clockSync.m * 1000.0 * .5) { // lost samples					 
					Console.WriteLine(deltaSamples + " Lost samples detected");
					clockSync.Reset();
					//clockSync.b = clockSync.b - deltaSamples;
				} else if (deltaSamples < -clockSync.m * 1000.0 * .5) { // extra samples
					Console.WriteLine(-deltaSamples + " Extra samples detected");
					clockSync.Reset();
				}
			}
			clockSync.UpdateClock(secount.NumSamples); // update the rt->sample mapping
			return secount;
		}

        /// <summary>
        /// Get the header from the FieldTrip buffer.
        /// </summary>
        /// <returns>The header.</returns>
		override public Header GetHeader()
		{
			Header hdr = base.GetHeader();
			clockSync.UpdateClock(hdr.NumSamples); // update the rt->sample mapping
			return hdr;
		}

        /// <summary>
        /// Connect to the FieldTrip buffer at the given address and port.
        /// </summary>
        /// <param name="address">The host where the FieldTrip buffer is running.</param>
        /// <param name="port">The port at which the FieldTrip buffer is running.</param>
        /// <returns></returns>
		override public bool Connect(string address, int port)
		{
			clockSync.Reset(); // reset old clock info (if any)
			return base.Connect(address, port);
		}

		//--------------------------------------------------------------------
		// New methods to do the clock syncronization
		public long GetSampOrPoll()
		{
			long sample = -1;
			bool dopoll = false;
			if (GetSampErr() > maxSampError || // error too big
			    Time > (long)(clockSync.Tlast) + updateInterval || // simply too long since we updated
			    clockSync.N < 8) { // Simply not enough points to believe we've got a good estimate
				dopoll = true;
			}
			if (GetSamp() < (long)(clockSync.Slast)) { // detected prediction before last known sample
				numWrong++; // increment count of number of times this has happened
				dopoll = true;
			} else {
				numWrong = 0;
			}
			if (Time < (long)(clockSync.Tlast) + minUpdateInterval) { // don't update too rapidly
				dopoll = false;
			}
			if (dopoll) { // poll buffer for current samples
				if (numWrong > 5) { 
					clockSync.Reset(); // reset clock if detected sysmetic error
					numWrong = 0;
				}
				long sampest = GetSamp();
				//Console.Write("Updating clock sync: SampErr " + getSampErr() + 
				//					  " getSamp " + sampest + " Slast " + clockSync.Slast);
				sample = Poll(0).NumSamples; // force update if error is too big
				//Console.WriteLine(" poll " + sample + " delta " + (sample-sampest));
			} else { // use the estimated time
				sample = (int)GetSamp();
			}
			return sample;
		}

		public long GetSamp()
		{
			return clockSync.GetSamp();
		}

		public long GetSamp(double time)
		{
			return clockSync.GetSamp(time);
		}

		public long GetSampErr()
		{
			return Math.Abs(clockSync.GetSampErr());
		}

		public double Time {
			get {
				return clockSync.GetTime();
			}
		}
		// time in milliseconds
		public SamplesEventsCount SyncClocks()
		{
			return	 SyncClocks(new int[] { 100, 100, 100, 100, 100, 100, 100, 100, 100 });
		}

		public SamplesEventsCount SyncClocks(int wait)
		{
			return	 SyncClocks(new int[] { wait });
		}

		public SamplesEventsCount SyncClocks(int[] wait)
		{
			clockSync.Reset();
			SamplesEventsCount ssc;
			ssc = Poll(0);
			for (int i = 0; i < wait.Length; i++) {			
				try {
					System.Threading.Thread.Sleep(wait[i]);
				} catch { // (InterruptedException e) 
				}
				ssc = Poll(0);				
			}
			return ssc;
		}

		public ClockSync ClockSync {
			get {
				return clockSync;
			}
		}
	}
}