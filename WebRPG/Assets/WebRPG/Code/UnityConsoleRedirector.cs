using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace WebRPG
{
    public class UnityConsoleRedirector : MonoBehaviour
    {
        private TextWriter _originalOut;
        private TextWriter _originalError;

        private void Awake()
        {
            _originalOut = Console.Out;
            _originalError = Console.Error;

            // Redirect standard out and standard error to Unity's Debug Log
            Console.SetOut(new UnityTextWriter(LogType.Log));
            Console.SetError(new UnityTextWriter(LogType.Error));
        }

        private void OnDestroy()
        {
            if (_originalOut != null)
            {
                Console.SetOut(_originalOut);
                _originalOut = null;
            }

            if (_originalError != null)
            {
                Console.SetError(_originalError);
                _originalError = null;
            }
        }

        private class UnityTextWriter : TextWriter
        {
            private readonly LogType _logType;
            private readonly StringBuilder _buffer = new StringBuilder();

            public override Encoding Encoding => Encoding.UTF8;

            public UnityTextWriter(LogType logType)
            {
                _logType = logType;
            }

            // Catch individual characters to build full lines
            public override void Write(char value)
            {
                if (value == '\n')
                {
                    FlushBuffer();
                }
                else if (value != '\r') // Ignore carriage returns
                {
                    _buffer.Append(value);
                }
            }

            // Catch string blocks / WriteLine directly
            public override void WriteLine(string value)
            {
                Log(value);
            }

            private void FlushBuffer()
            {
                if (_buffer.Length > 0)
                {
                    Log(_buffer.ToString());
                    _buffer.Clear();
                }
            }

            private void Log(string message)
            {
                if (_logType == LogType.Error)
                {
                    Debug.LogError(message);
                }
                else
                {
                    Debug.Log(message);
                }
            }
        }
    }
}
