using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;

/********************************* 패킷
 * CONNECT : 접속 성공
 * DISCONNECT : 접속 끊김
 * LOGIN : 로그인
 * LOGOUT : 로그아웃
 * USER : 유저 정보 ( 내가 아닌 유저 )
 * ADDUSER : 나 ( 본인 추가 )
 * MOVE : 이동
 * CHAT : 채팅
 */

public enum PROPER
{
    GENERAL,
    POLICE,
    THIEF
}
public enum COLOR
{
    WHITE,
    GREEN,
    YELLOW,
    ORANGE,
    BLU,
    PURPLE,
    RED,
    GRAY,
    BLACK
}


namespace Server
{
    class User
    {
        UserData userData = new UserData();
        static System.Timers.Timer tmr = new System.Timers.Timer();

        public const int maxPlayTime = 180;
        public static int mapNum = 0;
        public static int timeCount = maxPlayTime;

        /**** 유저가 가지고 있을 정보 (변수) ********************/
        string nickName = "";
        public int myIdx = 0;
        float posX = 0, posY = 0;
        public bool isLive = true;
        COLOR color = COLOR.WHITE;
        public PROPER proper = PROPER.GENERAL;
        MOVE_CONTROL myMove = MOVE_CONTROL.STOP;
        uint thiefActiveMass = 0;
        int policeActiveMass = 0;

        /****************************************************/


        public User(Socket socket)
        {
            if (tmr.Interval != 1000)
            {
                tmr.Interval = 1000;
                tmr.Elapsed += timeCheck;
                tmr.AutoReset = true;
            }

            myIdx = Server.userIdx;
            proper = PROPER.GENERAL;
            color = COLOR.WHITE;
            userData.workSocket = socket;

            userData.workSocket.BeginReceive(userData.buf, userData.recvLen, UserData.BUFFER_SIZE, 0, new AsyncCallback(ReadCallBack), userData);
            SendMsg("CONNECT");
        }

        /**
         * @brief 클라이언트로 보내는 패킷
         * @param result 결과
         */
        void ReadCallBack(IAsyncResult result)
        {
            try
            {
                Socket handler = userData.workSocket;
                int bytesRead = handler.EndReceive(result);

                if (bytesRead > 0)
                {
                    userData.recvLen += bytesRead;

                    while (true)
                    {
                        short len = 0;
                        Util.GetShort(userData.buf, 0, out len);

                        if (len > 0 && userData.recvLen >= len)
                        {
                            ParsePacket(len);
                            userData.recvLen -= len;

                            if (userData.recvLen > 0)
                            {
                                Buffer.BlockCopy(userData.buf, len, userData.buf, 0, userData.recvLen);
                            }
                            else
                            {
                                handler.BeginReceive(userData.buf, userData.recvLen, UserData.BUFFER_SIZE, 0, new AsyncCallback(ReadCallBack), userData);
                                break;
                            }
                        }
                        else
                        {
                            handler.BeginReceive(userData.buf, userData.recvLen, UserData.BUFFER_SIZE, 0, new AsyncCallback(ReadCallBack), userData);
                            break;
                        }
                    }
                }
                else
                {
                    handler.BeginReceive(userData.buf, userData.recvLen, UserData.BUFFER_SIZE, 0, new AsyncCallback(ReadCallBack), userData);
                }
            }
            catch (Exception ex)
            {
                short len = 0;
                Util.GetShort(userData.buf, 0, out len);
                Console.WriteLine(Encoding.UTF8.GetString(userData.buf, 2, len));
                Console.WriteLine("SOCKECT DISCONNECT");
                Console.WriteLine(ex.ToString());
            }
        }


        private static void timeCheck(Object source, System.Timers.ElapsedEventArgs e)
        {
            timeCount--;

            if (timeCount < 0)
            {
                timeCount = maxPlayTime;
                tmr.Stop();

                uint maxActive = 0;
                int mvpIdx = 0;
                for (int i = 0; i < Server.v_user.Count; i++)
                    if (Server.v_user[i] != null)
                        if (Server.v_user[i].proper.Equals(PROPER.THIEF) && Server.v_user[i].isLive)
                            if (maxActive < Server.v_user[i].thiefActiveMass)
                            {
                                maxActive = Server.v_user[i].thiefActiveMass;
                                mvpIdx = Server.v_user[i].myIdx;
                            }

                for (int i = 0; i < Server.v_user.Count; i++)
                    Server.v_user[i].SendMsg(string.Format("DONE:{0}:{1}", (int)PROPER.THIEF, mvpIdx));

                return;
            }

            Console.WriteLine("TC " + timeCount);
            for (int i = 0; i < Server.v_user.Count; i++)
            {
                Server.v_user[i].SendMsg(string.Format("TIME:{0}", timeCount));

                if (timeCount % 60 == 0 && !timeCount.Equals(0))
                    ChangeColor();
            }
        }

        /**
         * brief 패킷 분석
         * param len 길이
         */
        private void ParsePacket(int len)
        {
            string msg = Encoding.UTF8.GetString(userData.buf, 2, len - 2);
            string[] txt = msg.Split(':');      // 암호를 ':' 로 분리해서 읽음

            //Console.WriteLine(msg);

            /************* 기능이 추가되면 덧붙일 것 ***************/
            if (txt[0].Equals("MOVE"))
            {
                int idx = int.Parse(txt[1]);
                posX = float.Parse(txt[2]);
                posY = float.Parse(txt[3]);
                myMove = (MOVE_CONTROL)int.Parse(txt[4]);

                Move(idx);

                if (proper.Equals(PROPER.THIEF))
                    thiefActiveMass++;
            }
            else if (txt[0].Equals("CHAT"))
            {
                Console.WriteLine("MSG");
                Chat(txt[1], txt[2]);
            }
            else if (txt[0].Equals("KINEMATIC"))
            {
                for (int i = 0; i < Server.v_user.Count; i++)
                    Server.v_user[i].SendMsg(msg);
            }
            else if (txt[0].Equals("ATTACK"))
            {
                Console.WriteLine("ATTACK");
                Attack(int.Parse(txt[1]));
                policeActiveMass--;             // 아직 모호함
            }
            else if (txt[0].Equals("DIE"))
            {
                Die(int.Parse(txt[1]), int.Parse(txt[2]));
            }
            else if (txt[0].Equals("START"))
            {
                StartGame();
            }
            else if (txt[0].Equals("LOGIN"))
            {
                nickName = txt[1];
                Login(txt[2]);
                Console.WriteLine(txt[1] + " is Login.");
            }
            else if (txt[0].Equals("DISCONNECT"))
            {
                if (nickName.Length > 0)
                {
                    Console.WriteLine(nickName + " is Logout.");
                    Logout();
                }
                userData.workSocket.Shutdown(SocketShutdown.Both);
                userData.workSocket.Close();

                Server.RemoveUser(myIdx);

                if (Server.v_user.Count.Equals(0))
                {
                    timeCount = maxPlayTime;
                    tmr.Stop();
                    for (int j = 0; j < Server.v_user.Count; j++)
                        Server.v_user[j].SendMsg(string.Format("DIE:{0}:{1}", myIdx, myIdx));
                }
                else
                {
                    for (int j = 0; j < Server.v_user.Count; j++)
                        Server.v_user[j].SendMsg(string.Format("DIE:{0}:{1}", myIdx, myIdx));
                }
            }            
            else
            {
                //!< 이 부분에 들어오는 일이 있으면 안됨 (패킷 실수)
                Console.WriteLine("Un Correct Message ");
            }
        }

        /**
         * @brief 로그인
         */
        void Login(string version)
        {
            if ((timeCount < maxPlayTime && Server.v_user.Count > 9) || (!Server.version.Equals(version)))
            {
                SendMsg(string.Format("WAIT:{0}", mapNum));
                return;
            }

            for (int i = 0; i < Server.v_user.Count; i++)
            {
                //!< 내가 아닌 다른 유저에게
                if (Server.v_user[i] != this)
                {
                    /******** 유저 정보들을 이곳에 추가 *********/
                    SendMsg(string.Format("USER:{0}:{1}:{2}:{3}:{4}", Server.v_user[i].myIdx, Server.v_user[i].nickName, Server.v_user[i].posX, Server.v_user[i].posY, (int)Server.v_user[i].myMove));
                    // 현재 접속되 있는 유저 정보들을 방금 들어온 유저에게 전송

                    Server.v_user[i].SendMsg(string.Format("USER:{0}:{1}:{2}:{3}:{4}", myIdx, nickName, /*posX*/0, /*posY*/0, (int)MOVE_CONTROL.STOP));
                    // 기존에 접속해 있던 모든 유저들에게 내 정보 전송.

                    //if (timeCount < maxPlayTime)
                    //    Server.v_user[i].SendMsg(string.Format("WAIT:{0}", mapNum));
                }
                else
                {
                    SendMsg(string.Format("ADDUSER:{0}:{1}", myIdx, nickName));

                    //Console.WriteLine("WAIT : " + timeCount);
                    //if (timeCount < maxPlayTime)
                    //    SendMsg(string.Format("WAIT:{0}", mapNum));
                }
            }
        }

        /**
         * @brief 유저가 나가졌을때 다른 유저에게 이를 알림
         */
        void Logout()
        {
            int index = Server.v_user.IndexOf(this);

            for (int i = 0; i < Server.v_user.Count; i++)
                if (Server.v_user[i] != this)
                    Server.v_user[i].SendMsg(string.Format("LOGOUT:{0}", index));
        }

        /**
         * @brief 채팅
         * @param nick : 이름
         * @param msg : 내용
         */
        void Chat(string nick, string msg)
        {
            Console.WriteLine(string.Format("CHAT:{0}:{1}", nick, msg));
            for (int i = 0; i < Server.v_user.Count; i++)
                if (Server.v_user != null)
                    Server.v_user[i].SendMsg(string.Format("CHAT:{0}:{1}", nick, msg));
        }

        /**
         * @brief 사망
         * @param dIdx : 죽은 유저 idx
         * @param tIdx : 죽인 유저 idx
         */
        void Die(int dIdx, int tIdx)
        {
            Console.WriteLine("DIED");

            int thiefCount = 0, policeCount = 0;
            for (int j = 0; j < Server.v_user.Count; j++)
            {
                if (Server.v_user[j] != null)
                {
                    Server.v_user[j].SendMsg(string.Format("DIE:{0}:{1}", dIdx, tIdx));

                    if (Server.v_user[j].isLive)
                    {
                        if (dIdx.Equals(Server.v_user[j].myIdx))
                            Server.v_user[j].isLive = false;
                        else
                        {
                            if (Server.v_user[j].proper.Equals(PROPER.THIEF)) thiefCount++;
                            else if (Server.v_user[j].proper.Equals(PROPER.POLICE)) policeCount++;
                        }

                        if (Server.v_user[j].myIdx.Equals(tIdx) && !dIdx.Equals(tIdx))
                            Server.v_user[j].policeActiveMass += 5;
                    }
                }
            }

            if (thiefCount.Equals(0))
            {
                timeCount = maxPlayTime;
                tmr.Stop();

                int maxActive = -10, mvpIdx = 0;
                for (int i = 0; i < Server.v_user.Count; i++)
                    if (Server.v_user[i] != null)
                        if (Server.v_user[i].proper.Equals(PROPER.POLICE) && Server.v_user[i].isLive)
                            if (maxActive < Server.v_user[i].policeActiveMass)
                            {
                                maxActive = Server.v_user[i].policeActiveMass;
                                mvpIdx = Server.v_user[i].myIdx;
                            }

                for (int i = 0; i < Server.v_user.Count; i++)
                    Server.v_user[i].SendMsg(string.Format("DONE:{0}:{1}", (int)PROPER.POLICE, mvpIdx));
            }
            else if (policeCount.Equals(0))
            {
                timeCount = maxPlayTime;
                tmr.Stop();

                uint maxActive = 0;
                int mvpIdx = 0;
                for (int i = 0; i < Server.v_user.Count; i++)
                    if (Server.v_user[i] != null)
                        if (Server.v_user[i].proper.Equals(PROPER.THIEF) && Server.v_user[i].isLive)
                            if (maxActive < Server.v_user[i].thiefActiveMass)
                            {
                                maxActive = Server.v_user[i].thiefActiveMass;
                                mvpIdx = Server.v_user[i].myIdx;
                            }

                for (int i = 0; i < Server.v_user.Count; i++)
                    Server.v_user[i].SendMsg(string.Format("DONE:{0}:{1}", (int)PROPER.THIEF, mvpIdx));
            }
        }

        /**
         * @brief 게임 시작 (직업 나눠줌)
         */
        static void StartGame()
        {
            mapNum = Server.rand.Next(0, 6);
            Console.WriteLine(string.Format("START !!! : {0}", mapNum));


            for (int j = 0; j < Server.v_user.Count; j++)
                if (Server.v_user[j] != null)
                {
                    Server.v_user[j].SendMsg(string.Format("START:{0}", mapNum));
                    Server.v_user[j].isLive = true;
                    Server.v_user[j].policeActiveMass = 0;
                    Server.v_user[j].thiefActiveMass = 0;
                }

            int memCount = 0;
            for (int i = 0; i < Server.v_user.Count; i++)
                if (Server.v_user[i] != null)
                    memCount++;

            PROPER[] proArr = new PROPER[memCount];

            for (int i = 0; i < memCount / 3; i++)
                proArr[i] = PROPER.POLICE;
            for (int i = 0; i < memCount - (memCount / 3); i++)
                proArr[(memCount / 3) + i] = PROPER.THIEF;

            int mIdx = 0;
            for (int i = 0; i < Server.v_user.Count; i++)
            {
                if (Server.v_user[i] != null)
                {
                    int colorT = Server.rand.Next(0, 9);
                    int pIdx = Server.rand.Next(0, memCount - (mIdx++));
                    for (int j = 0; j < Server.v_user.Count; j++)
                        if (Server.v_user[j] != null)
                            Server.v_user[j].SendMsg(string.Format("PROPER:{0}:{1}:{2}", Server.v_user[i].myIdx, (int)proArr[pIdx], (int)colorT));

                    PROPER tp = proArr[memCount - mIdx];
                    proArr[memCount - mIdx] = proArr[pIdx];
                    proArr[pIdx] = tp;
                }
            }

            //int police = memCount / 3;
            //int thief = memCount - police;

            //for (int i = 0; i < Server.v_user.Count; i++)
            //{
            //    int colorT = Server.rand.Next(0, 9);

            //    if (Server.v_user[i] != null)
            //    {
            //        if (thief.Equals(0))
            //        {
            //            police--;
            //            Server.v_user[i].proper = PROPER.POLICE;

            //            for (int j = 0; j < Server.v_user.Count; j++)
            //                if (Server.v_user[j] != null)
            //                    Server.v_user[j].SendMsg(string.Format("PROPER:{0}:{1}:{2}", Server.v_user[i].myIdx, (int)PROPER.POLICE, (int)colorT));
            //        }
            //        else if (police > 0)
            //        {
            //            if (Server.rand.Next(0, 2) == 0)
            //            {
            //                police--;
            //                Server.v_user[i].proper = PROPER.POLICE;

            //                for (int j = 0; j < Server.v_user.Count; j++)
            //                    if (Server.v_user[j] != null)
            //                        Server.v_user[j].SendMsg(string.Format("PROPER:{0}:{1}:{2}", Server.v_user[i].myIdx, (int)PROPER.POLICE, (int)colorT));
            //            }
            //            else
            //            {
            //                thief--;
            //                Server.v_user[i].proper = PROPER.THIEF;

            //                for (int j = 0; j < Server.v_user.Count; j++)
            //                    if (Server.v_user[j] != null)
            //                        Server.v_user[j].SendMsg(string.Format("PROPER:{0}:{1}:{2}", Server.v_user[i].myIdx, (int)PROPER.THIEF, (int)colorT));
            //            }
            //        }
            //        else
            //        {
            //            thief--;
            //            Server.v_user[i].proper = PROPER.THIEF;

            //            for (int j = 0; j < Server.v_user.Count; j++)
            //                if (Server.v_user[j] != null)
            //                    Server.v_user[j].SendMsg(string.Format("PROPER:{0}:{1}:{2}", Server.v_user[i].myIdx, (int)PROPER.THIEF, (int)colorT));
            //        }
            //    }
            //}

            tmr.Start();
        }

        /**
         * @brief 색바꾸기
         */
        static void ChangeColor()
        {
            for (int i = 0; i < Server.v_user.Count; i++)
            {
                if (Server.v_user[i] != null)
                {
                    if (Server.v_user[i].proper == PROPER.THIEF)
                    {
                        int colorT;
                        if (timeCount.Equals(60))
                            colorT = Server.rand.Next(0, 8);
                        else
                            colorT = Server.rand.Next(0, 9);

                        for (int j = 0; j < Server.v_user.Count; j++)
                            if (Server.v_user[j] != null)
                                Server.v_user[j].SendMsg(string.Format("CHANGE:{0}:{1}", Server.v_user[i].myIdx, (int)colorT));
                    }
                }
            }
        }

        /**
         * @brief 공격
         * @param 공격한 유저의 idx
         */
        void Attack(int idx)
        {
            for (int j = 0; j < Server.v_user.Count; j++)
                if (Server.v_user[j] != null)
                    Server.v_user[j].SendMsg(string.Format("ATTACK:{0}", idx));
        }

        /**
         * @brief 이동
         */
        void Move(int idx)
        {
            for (int i = 0; i < Server.v_user.Count; i++)
            {
                if (Server.v_user[i] != null)
                    if (Server.v_user[i] != this)
                        Server.v_user[i].SendMsg(string.Format("MOVE:{0}:{1}:{2}:{3}", idx, posX, posY, (int)myMove)); // 내 인덱스 번호와 현재 위치 이동할 방향을 보낸다.
            }
        }

        /**
         * @brief 클라이언트로 보내는 패킷
         * @param msg 클라이언트가 인식할 메세지, 일종의 암호 (?)
         */
        void SendMsg(string msg)
        {
            try
            {
                if (userData.workSocket != null && userData.workSocket.Connected)
                {
                    byte[] buff = new byte[4096];
                    Buffer.BlockCopy(ShortToByte(Encoding.UTF8.GetBytes(msg).Length + 2), 0, buff, 0, 2);
                    Buffer.BlockCopy(Encoding.UTF8.GetBytes(msg), 0, buff, 2, Encoding.UTF8.GetBytes(msg).Length);
                    userData.workSocket.Send(buff, Encoding.UTF8.GetBytes(msg).Length + 2, 0);
                }
            }
            catch (System.Exception e)
            {
                if (nickName.Length > 0) Logout();

                userData.workSocket.Shutdown(SocketShutdown.Both);
                userData.workSocket.Close();

                Server.RemoveUser(myIdx);

                Console.WriteLine("SendMsg Error : " + e.Message);
            }
        }

        /**
         * @brief 클라이언트로 보내는 패킷
         */
        byte[] ShortToByte(int val)
        {
            byte[] temp = new byte[2];
            temp[1] = (byte)((val & 0x0000ff00) >> 8);
            temp[0] = (byte)((val & 0x000000ff));
            return temp;
        }
    }
}
