using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;


namespace ThreadWar
{
    public class Program
    {
        internal static Thread badGuyThread;
        internal static Thread moveThread;
        internal static EventWaitHandle startEventWaitHandle;
        internal static Mutex screenLock = new Mutex();
        internal static Semaphore bulletSemaphore;

        internal static List<Thread> badGuysTheads = new List<Thread>();
        internal static List<Thread> bulletsThreads = new List<Thread>();

        internal static bool end;
        internal static long hits;
        internal static long miss;
        internal static char[] badChar = { '-', '\\', '|', '/' };

        public const int ScMinimize = 0xF020;
        public const int ScMaximize = 0xF030;
        public const int ScSize = 0xF000;

        internal static int cHeight = 25;
        internal static int cWidth = 80;
        internal static char[,] cMatrixChars = new char[cHeight + 1, cWidth + 1];
        private static int _oX, _oY;

        [DllImport("kernel32.dll", ExactSpelling = true)]
        private static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int MessageBox(IntPtr hWnd, string lpText, string lpCaption, uint uType);

        [DllImport("user32.dll")]
        public static extern int DeleteMenu(IntPtr hMenu, int nPosition, int wFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);

        private static int Random(int n0, int n1)
        {
            return new Random().Next(n0, n1);
        }

        private static void WriteAt(int x, int y, char res)
        {
            screenLock.WaitOne();
            Console.SetCursorPosition(x, y);
            Console.Write(res);
            cMatrixChars[y, x] = res;
            screenLock.ReleaseMutex();
        }

        private static char GetAt(int x, int y)
        {
            screenLock.WaitOne();
            var res = cMatrixChars[y, x];
            screenLock.ReleaseMutex();
            return res;
        }

        private static void Score()
        {
            if (miss >= 20)
            {
                Console.Title = $"Война потоков - Врагов подбито: {hits}, Врагов пропущено: {miss}";
                badGuyThread.Abort();
                end = true;
                MessageBox(IntPtr.Zero, $"Игра окончена!\n Врагов подбито: {hits}, Врагов пропущено: {miss}", "Thread War", 0);
            }
            else
                Console.Title = $"Война потоков - Врагов подбито: {hits}, Врагов пропущено: {miss}";
        }

        private static void BadGuy()
        {
            bool hitme = false;
            int left = 1, right = cWidth - 1;
            int y = Random(0, 20);
            int x = Random(0, 2) == 0 ? left : right;
            int dir = x == 1 ? 1 : -1;
            while (dir == 1 && x != cWidth - 2 || dir == -1 && x != 2)
            {
                if (end)
                    return;
                WriteAt(x, y, badChar[x % 4]);
                for (int i = 0; i < 10; i++)
                {
                    Thread.Sleep(40);
                    if (GetAt(x, y) == '*')
                    {
                        hitme = true;
                        Console.Beep();
                        break;
                    }
                }
                WriteAt(x, y, ' ');
                if (hitme)
                {
                    Interlocked.Increment(ref hits);
                    Score();
                    return;
                }
                x += dir;
            }
            Interlocked.Increment(ref miss);
            Score();
        }

        private static void BadGuys()
        {
            startEventWaitHandle.WaitOne(1000);
            while (true)
            {
                if (end)
                    break;
                if (Random(0, 100) < (hits + miss) / 25 + 20)
                {
                    badGuysTheads.Add(new Thread(BadGuy));
                    badGuysTheads[badGuysTheads.Count - 1].Start();
                }
                Thread.Sleep(1000);
            }
        }

        private static void Bullet()
        {
            bulletSemaphore.WaitOne();
            int x = _oX;
            int y = _oY;
            if (GetAt(x, y) == '*')
                return;
            
            while (--y != -1)
            {
                if (end)
                    return;
                WriteAt(x, y, '*');
                Thread.Sleep(100);
                WriteAt(x, y, ' ');
            }
            bulletSemaphore.Release();
        }

        private static void InitProg()
        {
            IntPtr handle = GetConsoleWindow();
            IntPtr sysMenu = GetSystemMenu(handle, false);
            if (handle != IntPtr.Zero)
            {
                DeleteMenu(sysMenu, ScMinimize, 0);
                DeleteMenu(sysMenu, ScMaximize, 0);
                DeleteMenu(sysMenu, ScSize, 0);
            }
            Console.SetWindowSize(cWidth + 1, cHeight + 1);
            Console.CursorVisible = false;
            Console.BufferHeight = cHeight + 1;
            Console.BufferWidth = cWidth + 1;

            for (int i = 0; i < cHeight; i++)
                for (int j = 1; j <= cWidth; j++)
                {
                    Console.SetCursorPosition(j, i);
                    Console.Write(' ');
                    cMatrixChars[i, j] = ' ';
                }
        }

        static void Move()
        {
            int x = cWidth / 2;
            int y = cHeight;
            startEventWaitHandle.Set();
            while (true)
            {
                WriteAt(x, y, '|');
                switch (Console.ReadKey(true).Key)
                {
                    case ConsoleKey.Spacebar:
                        _oX = x;
                        _oY = y - 1;
                        bulletsThreads.Add(new Thread(Bullet));
                        bulletsThreads[bulletsThreads.Count - 1].Start();
                        break;

                    case ConsoleKey.LeftArrow:
                        if (x - 1 >= 0)
                        {
                            WriteAt(x, y, ' ');
                            x--;
                        }
                        break;
                    case ConsoleKey.RightArrow:
                        if (x + 1 <= cWidth)
                        {
                            WriteAt(x, y, ' ');
                            x++;
                        }
                        break;
                }
            }
        }

        private static void Main()
        {
            bulletSemaphore = new Semaphore(4, 4);
            InitProg();
            startEventWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
            Score();

            badGuyThread = new Thread(BadGuys);
            badGuyThread.Start();

            moveThread = new Thread(Move);
            moveThread.Start();
        }
    }
}
