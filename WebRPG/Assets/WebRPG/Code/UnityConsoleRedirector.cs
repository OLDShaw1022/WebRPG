using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace WebRPG
{
    public class UnityConsoleRedirector : MonoBehaviour
    {
        private TextWriter _originalStandardOutput;
        private TextWriter _originalError;

        private void Awake()
        {
            _originalStandardOutput = Console.Out;
            _originalError = Console.Error;
            Console.SetOut(new UnityTextWriter(LogType.Log));
            Console.SetError(new UnityTextWriter(LogType.Error));
        }

        private void OnDestroy()
        {
            if (_originalStandardOutput != null)
            {
                Console.SetOut(_originalStandardOutput);
                _originalStandardOutput = null;
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
            public override void Write(char value)
            {
                if (value == '\n')
                {
                    FlushBuffer();
                }
                else if (value != '\r')
                {
                    _buffer.Append(value);
                }
            }
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
