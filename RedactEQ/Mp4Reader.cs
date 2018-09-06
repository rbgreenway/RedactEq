﻿using Equature.Integration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace VideoTools
{
    public struct ProgressStruct
    {
        public long timestamp;  // current frame position within video (milliseconds)
        public long length;    // total length of video (milliseconds)

        public byte[] data;
        public bool key;
        public int width;
        public int height;
        public ProgressStruct(long tstamp, long leng, byte[] imageData, bool isKeyFrame, int w, int h)
        {
            timestamp = tstamp;
            length = leng;
            data = imageData;
            width = w;
            height = h;
            key = isKeyFrame;
        }
    }

    public class Mp4Reader
    {
        private string m_errorMsg;
        int m_frameCount;



        // Call this function to read/decode an MP4 file.
        // the handler parameter is simply a function defined elsewhere as:
        //      void NewFrame(ProgressStruct progress)  // this function gets called once per frame decoded
        //      {   
        //          <put whatever you want here to...like displaying the frames, feeding them to a dnn, or whatever>
        //      }
        //  
        public async void StartPlayback(string filename, Action<ProgressStruct> newFrameHandler,
            int decodeWidth, int decodeHeight, 
            CancellationTokenSource tokenSource, 
            WPFTools.PauseTokenSource pauseTokenSource,
            bool paceOutput)
        {
            //construct Progress<T>, passing ReportProgress as the Action<T> 
            var progressIndicator = new Progress<ProgressStruct>(newFrameHandler);

            //call async method
            long position = await PlayMp4FileAsync(filename, decodeWidth, decodeHeight,
                progressIndicator, tokenSource.Token, pauseTokenSource.Token, paceOutput);
        }



        async Task<long> PlayMp4FileAsync(string path, int targetWidth, int targetHeight,
            IProgress<ProgressStruct> progress, 
            CancellationToken token, WPFTools.PauseToken pauseToken,
            bool paceOutput)
        {
            // path - filename/path to Mp4 file
            // startingAt - point in time to start decoding/playback, given in milliseconds
            // targetWidth, targetHeight - desired pixel dimension of decoded frames
            // paceOutput - flag indicating whether to pace the output, using frame 
            //              timestamps and framerate, so that playback is at video rate

            long count = -1;

            if (File.Exists(path))
            {
                count = await Task.Run<long>(async () =>
                {
                    long timestamp = 0;
                    Stopwatch sw = new Stopwatch();

                    IntPtr mp4Reader = Mp4.CreateMp4Reader(path);

                    if (mp4Reader != IntPtr.Zero)
                    {
                        try
                        {
                            long durationMilliseconds;
                            double frameRate;
                            long timestampDelta;
                            long timestampWindow;
                            int height;
                            int width;
                            int sampleCount;

                            // Get the video metadata from the file
                            if (Mp4.GetVideoProperties(mp4Reader, out durationMilliseconds, out frameRate, out width, out height, out sampleCount))
                            {
                                timestampDelta = (long)(1000.0f / frameRate);
                                timestampWindow = timestampDelta / 2;


                                // Ask the decoder to resample to targetWidth x targetHeight, and assume 24-bit RGB colorspace
                                byte[] frame = new byte[targetWidth * targetHeight * 3];
                                bool key;
                               
                                ProgressStruct prog;

                                m_frameCount = 0;

                                sw.Start();

                                while (true)
                                {
                                    timestamp = Mp4.GetNextVideoFrame(mp4Reader, frame, out key, targetWidth, targetHeight);

                                    if (timestamp == -1)   // EOF                             
                                    {
                                        break;
                                    }

                                    if (token.IsCancellationRequested)
                                    {
                                        // pause or stop requested
                                        break;
                                    }

                                    byte[] frameCopy = new byte[targetWidth * targetHeight * 3];
                                    Buffer.BlockCopy(frame, 0, frameCopy, 0, targetWidth * targetHeight * 3);
                                    prog = new ProgressStruct(timestamp, durationMilliseconds, frameCopy, key, targetWidth, targetHeight);

                                    if (progress != null && prog.data != null)
                                    {
                                        // if we're in playback mode, pace the frames appropriately
                                        if (paceOutput)
                                        {
                                            while (sw.ElapsedMilliseconds < timestampDelta)
                                            {
                                                Thread.Sleep(1);
                                            }
                                            sw.Restart();
                                        }

                                        m_frameCount++;

                                        // send frame to UI thread
                                        progress.Report(prog);
                                    }

                                    await pauseToken.WaitWhilePausedAsync();
                                }
                            }

                        }
                        catch (Exception ex)
                        {
                            m_errorMsg = ex.Message;
                            timestamp = -2;  // indicating an exception occurred
                        }
                        finally
                        {
                            Mp4.DestroyMp4Reader(mp4Reader);
                            progress.Report(new ProgressStruct(-1,0,null,false,0,0)); // signal that the plater stopped (timestamp = -1)
                        }
                    }
                    else
                    {
                        timestamp = -3;
                        m_errorMsg = "Could not open file.";
                    }

                    return timestamp;

                }, token);

            } // END File.Exists

            return count;
        }


        public string GetLastError()
        {
            return m_errorMsg;
        }


    }
}
