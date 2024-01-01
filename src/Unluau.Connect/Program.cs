// Copyright (c) Valence. All Rights Reserved.
// Licensed under the Apache License, Version 2.0

using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.IO;
using System.Threading;

namespace Unluau.CLI
{
    class Program
    {
        private static Timer _timer;
        private static readonly int TimeoutMilliseconds = 30000;

        static Socket CreateSocket()
        {
            Socket socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            socket.Bind(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 13370));

            return socket;
        }

        static void HandleRequest(Socket request)
        {
            //Console.WriteLine("getting sig");
            byte[] sigBuffer = new byte[15];
            request.Receive(sigBuffer);
            //Console.WriteLine("got sig");

            byte[] signature = Encoding.ASCII.GetBytes("IayingStan#8292");
            if (sigBuffer == signature)
            {
                /*
                Console.WriteLine(System.Text.Encoding.Default.GetString(sigBuffer));
                Console.WriteLine(sigBuffer.Length);
                for (int i = 0; i < sigBuffer.Length; i++)
                {
                    Console.WriteLine(i + " = " + sigBuffer[i]);
                }

                Console.WriteLine(System.Text.Encoding.Default.GetString(signature));
                Console.WriteLine(signature.Length);
                for (int i = 0; i < sigBuffer.Length; i++)
                {
                    Console.WriteLine(i + " = " + sigBuffer[i]);
                }
                */

                byte[] data = Encoding.UTF8.GetBytes("NAn unexpected error occured.");
                request.Send(data);
                request.Close();

                return;
            }

            //Console.WriteLine("getting length");
            byte[] lengthBuff = new byte[4];
            request.Receive(lengthBuff);

            UInt32 length = BitConverter.ToUInt32(lengthBuff);
            //Console.WriteLine("measured length " + length + " from " + lengthBuff[0] + "," + lengthBuff[1] + "," + lengthBuff[2] + "," + lengthBuff[3]);
            if(length == 0)
            {
                byte[] data = Encoding.ASCII.GetBytes("NNo bytecode was provided.");
                request.Send(data);
                request.Close();

                return;
            }

            byte[] bytecode = new byte[length];
            request.Receive(bytecode);

            // setup decompiler options
            DecompilerOptions options = new DecompilerOptions();
            options.RenameUpvalues = true;
            options.VariableNameGuessing = true;
            options.PerferStringInterpolation = true;
            options.Encoding = OpCodeEncoding.Client;

            // setup output stream
            Stream outputStream = new MemoryStream();
            StreamReader outputReader = new StreamReader(outputStream);
            StreamWriter outputWriter = new StreamWriter(outputStream);
            options.Output = new Output(outputWriter);

            // setup input stream
            Stream inputStream = new MemoryStream();
            inputStream.Write(bytecode);
            inputStream.Position = 0; // why the fuck is this a feature dear god C# is retarded

            // decompile
            try
            {
                Decompiler decompiler = new Decompiler(inputStream, options);
                outputStream.Write(Encoding.ASCII.GetBytes(decompiler.Dissasemble() + "\n\n"));
                try
                {
                    decompiler.Decompile();
                }
                catch (DecompilerException e)
                {
                    outputStream.Position = 0;
                    string response = outputReader.ReadToEnd();

                    request.Send(Encoding.ASCII.GetBytes("Y"));
                    request.Send(BitConverter.GetBytes(BinaryPrimitives.ReverseEndianness(response.Length)));
                    request.Send(Encoding.ASCII.GetBytes(response + "\n\nError while decompiling \"" + e.Message + "\""));
                    request.Close();

                    return;
                }
                catch (Exception e)
                {
                    outputStream.Position = 0;
                    string response = outputReader.ReadToEnd();

                    request.Send(Encoding.ASCII.GetBytes("Y"));
                    request.Send(BitConverter.GetBytes(BinaryPrimitives.ReverseEndianness(response.Length)));
                    request.Send(Encoding.ASCII.GetBytes(response + "\n\nUnexpected error while decompiling"));
                    request.Close();

                    return;
                }
            }
            catch (DecompilerException e)
            {
                byte[] data = Encoding.ASCII.GetBytes("NError while decompiling \"" + e.Message + "\"");
                request.Send(data);
                request.Close();

                return;
            }
            catch (Exception e)
            {
                byte[] data = Encoding.ASCII.GetBytes("NUnexpected error while decompiling");
                request.Send(data);
                request.Close();

                return;
            }

            // FUCK CREATORS OF C# LIBRARIES THEY ARE RETARDS
            outputStream.Position = 0;

            // get output
            String outputString = outputReader.ReadToEnd();
            outputWriter.Close();
            outputStream.Close();

            // fail check
            if(outputString.Length == 0)
            {
                byte[] data = Encoding.ASCII.GetBytes("NNo response from decompiler.");
                request.Send(data);
                request.Close();

                return;
            }

            // send response
            Console.WriteLine("final output of size " + outputString.Length);
            Console.WriteLine("final output: " + outputString);
            request.Send(Encoding.ASCII.GetBytes("Y"));
            request.Send(BitConverter.GetBytes(BinaryPrimitives.ReverseEndianness(outputString.Length)));
            request.Send(Encoding.ASCII.GetBytes(outputString));

            request.Close();
        }

        static void Main(string[] args)
        {
            _timer = new Timer(OnTimeout, null, TimeoutMilliseconds, Timeout.Infinite);

            Socket socket = CreateSocket();
            socket.Listen();

            //Console.WriteLine("Socket waiting for decompilation requests");
            while (true)
            {
                //Console.WriteLine("waiting");
                Socket request = socket.Accept();

                // A request has been received, so reset the timer
                _timer.Change(TimeoutMilliseconds, Timeout.Infinite);

                //Console.WriteLine("received");
                HandleRequest(request);
                //Console.WriteLine("handled");
            }
        }

        private static void OnTimeout(object state)
        {
            // Timeout has occurred, no request was received in the allowed time, close the program
            Environment.Exit(1); // Exit with an error code
        }
    }
}
