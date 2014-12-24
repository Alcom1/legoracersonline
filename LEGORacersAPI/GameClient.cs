using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace LEGORacersAPI
{
    /// <summary>
    /// Represents a skeleton for a LEGO Racers game client.
    /// </summary>
    public abstract class GameClient
    {
        /// <summary>
        /// Specifies the formatted client name.
        /// </summary>
        protected string CLIENT_FORMATTED_NAME = "Unknown";
	    
        /// <summary>
        /// The base address to load RRB files.
        /// </summary>
		protected int LOAD_RRB_BASE;

        /// <summary>
        /// The Power-up function base address.
        /// </summary>
        protected int POWERUP_FUNCTION_BASE;

        /// <summary>
        /// The Red Power-up base address.
        /// </summary>
		protected int POWERUP_RED_ADDRESS;

        /// <summary>
        /// The Blue Power-up base address.
        /// </summary>
		protected int POWERUP_BLUE_ADDRESS;

        /// <summary>
        /// The Green Power-up base address.
        /// </summary>
		protected int POWERUP_GREEN_ADDRESS;

        /// <summary>
        /// The Yellow Power-up base address.
        /// </summary>
		protected int POWERUP_YELLOW_ADDRESS;

        /// <summary>
        /// The base address to continue running in the background.
        /// </summary>
        protected int RUN_IN_BACKGROUND_ADDRESS;

        /// <summary>
        /// The base address of the number of AI racers.
        /// </summary>
		protected int AI_COUNT_ADDRESS;

		protected int MAINMENU_BUTTONS_BASE;
			
		protected int MENU_BASE;
		protected int TARGETMENU_ECX_OFFSET;
		protected int TARGETMENU_ESI_OFFSET;
		protected int CURRENT_MENU_OFFSET;
        protected int[] SELECTED_RACE_OFFSETS;
        protected int[] SELECTED_CIRCUIT_OFFSETS;
        protected int[] CIRCUIT_BASE_OFFSETS;
        protected int[] MENUSTRINGS_ECX_OFFSETS;
        protected int[] MENUSTRINGS_FILESTART_OFFSETS;

        protected int DRIVER_BASE;
        protected int[] PLAYER_BASE_OFFSETS;
        protected int[] ENEMY_1_BASE_OFFSETS;
        protected int[] ENEMY_2_BASE_OFFSETS;
        protected int[] ENEMY_3_BASE_OFFSETS;
        protected int[] ENEMY_4_BASE_OFFSETS;
		protected int[] ENEMY_5_BASE_OFFSETS;

        protected int DRIVER_OFFSET_COORDINATE_X;
        protected int DRIVER_OFFSET_COORDINATE_Y;
        protected int DRIVER_OFFSET_COORDINATE_Z;
        protected int DRIVER_OFFSET_SPEED_X;
        protected int DRIVER_OFFSET_SPEED_Y;
        protected int DRIVER_OFFSET_SPEED_Z;
        protected int DRIVER_OFFSET_VECTOR_X1;
        protected int DRIVER_OFFSET_VECTOR_Y1;
        protected int DRIVER_OFFSET_VECTOR_Z1;
        protected int DRIVER_OFFSET_VECTOR_X2;
        protected int DRIVER_OFFSET_VECTOR_Y2;
        protected int DRIVER_OFFSET_VECTOR_Z2;
        protected int DRIVER_OFFSET_BRICK;
        protected int DRIVER_OFFSET_WHITEBRICKS;

        private bool initialized;
        private bool active;
        private bool loadRRB;
        private bool enableAIPowerUps;
        private MemoryManager memoryManager;
        private Thread initializeThread;
        protected Process process;

        /// <summary>
        /// Gets the local in-game player.
        /// </summary>
        public Player Player { get; private set; }

        /// <summary>
        /// Gets the local in-game opponents.
        /// </summary>
        public Opponent[] Opponents { get; private set; }

        /// <summary>
        /// Gets the game clients formatted name.
        /// </summary>
        public string FormattedName
        {
            get
            {
                return CLIENT_FORMATTED_NAME;
            }
        }

        /// <summary>
        /// Gets or sets the value whether to load RRB (AI path) files.
        /// </summary>
        public bool LoadRRB
        {
            get
            {
                return loadRRB;
            }
            set
            {
                if (initialized)
                {
                    loadRRB = value;

                    SetLoadRRB(value);
                }
            }
        }

        /// <summary>
        /// Gets or sets the value whether AI drivers use the Power-ups they carry.
        /// </summary>
        public bool AIUsePowerUps
        {
            get
            {
                return enableAIPowerUps;
            }
            set
            {
                if (initialized)
                {
                    enableAIPowerUps = value;

                    SetAIUsePowerUps(value);
                }
            }
        }

        /// <summary>
        /// Gets or sets the value whether the game will continue to run in the background while minimized.
        /// </summary>
        public bool RunInBackground
        {
            get
            {
                return initialized ? memoryManager.ReadByte(RUN_IN_BACKGROUND_ADDRESS) == 0xEB : false;
            }
            set
            {
                if (initialized)
                {
                    if (value)
                    {
                        memoryManager.WriteByte(RUN_IN_BACKGROUND_ADDRESS, 0xEB);
                    }
                    else
                    {
                        memoryManager.WriteByte(RUN_IN_BACKGROUND_ADDRESS, 0x75);
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the amount of AI drivers in the current race.
        /// </summary>
        public int AIDriversAmount
        {
            get
            {
                return initialized ? memoryManager.ReadInt(memoryManager.ReadInt(0x004C4914) + 0x598) : 0;
            }
            set
            {
                if (initialized && value >= 0 && value <= 5)
                {
                    memoryManager.WriteInt(AI_COUNT_ADDRESS, value);
                }
            }
        }

        /// <summary>
        /// Gets the value whether a race is currently running.
        /// </summary>
        public bool IsRaceRunning
        {
            get
            {
                return initialized ? memoryManager.ReadInt(0x0018E61C) == 1 : false;
            }
        }

        /// <summary>
        /// Gets the value whether a race is currently paused.
        /// </summary>
        public bool Paused
        {
            get
            {
                return initialized ? memoryManager.ReadByte(0x4C5398) == 1 : false;
            }
        }

        /// <summary>
        /// Gets the value of the API initialization.
        /// </summary>
        public InitializedType InitializedType { get; private set; }

        public delegate void InitializedHandler(InitializedType type);
        public event InitializedHandler Initialized;

        protected void OnInitialized(InitializedType type)
        {
            if (InitializedType == InitializedType.Core)
            {
                InitializedType = InitializedType.Both;
            }

            if (Initialized != null)
            {
                Initialized(type);
            }
        }

        public GameClient()
        {
            initialized = false;
            InitializedType = InitializedType.None;
            initializeThread = new Thread(Initialize);
            initializeThread.Start();
        }

        /// <summary>
        /// Safely unloads the API.
        /// </summary>
        public void Unload()
        {
            active = false;

            if (Player != null)
            {
                Player.Unload();
            }

            foreach (Opponent opponent in Opponents)
            {
                if (opponent != null)
                {
                    opponent.Unload();
                }
            }
        }

        /// <summary>
        /// Initializes the in-game objects so they can be managed using the API.
        /// </summary>
        private void Initialize()
        {
            try
            {
                active = true;
                memoryManager = new MemoryManager(process);

                while (!initialized && active)
                {
                    if (GetCurrentMenu() != Menu.Loading && GetCurrentMenu() != Menu.Initializing)
                    {
                        initialized = true;

                        // Core functionality is now initialized
                        InitializedType = InitializedType.Core;

                        Initialized(InitializedType.Core);

                        while (active && !IsRaceRunning)
                        {
                            // Wait until a race has been started so the drivers can be initialized

                            Thread.Sleep((int)Settings.RefreshRate);
                        }

                        do
                        {
                            // Reset the player to a null value to prevent a memory overflow
                            Player = null;

                            // There is a small time between the starting of a race and the actual start,
                            // so the thread will continue to run for a small time until the local players X-coordinate
                            // is actually filled with correct data, which is a good way to test if the race has actually been started.

                            Player = new Player(memoryManager, DRIVER_BASE, PLAYER_BASE_OFFSETS, DRIVER_OFFSET_COORDINATE_X, DRIVER_OFFSET_COORDINATE_Y, DRIVER_OFFSET_COORDINATE_Z, DRIVER_OFFSET_SPEED_X, DRIVER_OFFSET_SPEED_Y, DRIVER_OFFSET_SPEED_Z, DRIVER_OFFSET_VECTOR_X1, DRIVER_OFFSET_VECTOR_Y1, DRIVER_OFFSET_VECTOR_Z1, DRIVER_OFFSET_VECTOR_X2, DRIVER_OFFSET_VECTOR_Y2, DRIVER_OFFSET_VECTOR_Z2, DRIVER_OFFSET_BRICK, DRIVER_OFFSET_WHITEBRICKS, POWERUP_RED_ADDRESS, POWERUP_BLUE_ADDRESS, POWERUP_GREEN_ADDRESS, POWERUP_YELLOW_ADDRESS);

                            Thread.Sleep((int)Settings.RefreshRate);
                        }
                        while (active && (Player == null || Player.X == 0));

                        Opponents = new Opponent[5];

                        Opponents[0] = new Opponent(memoryManager, DRIVER_BASE, ENEMY_1_BASE_OFFSETS, DRIVER_OFFSET_COORDINATE_X, DRIVER_OFFSET_COORDINATE_Y, DRIVER_OFFSET_COORDINATE_Z, DRIVER_OFFSET_SPEED_X, DRIVER_OFFSET_SPEED_Y, DRIVER_OFFSET_SPEED_Z, DRIVER_OFFSET_VECTOR_X1, DRIVER_OFFSET_VECTOR_Y1, DRIVER_OFFSET_VECTOR_Z1, DRIVER_OFFSET_VECTOR_X2, DRIVER_OFFSET_VECTOR_Y2, DRIVER_OFFSET_VECTOR_Z2, DRIVER_OFFSET_BRICK, DRIVER_OFFSET_WHITEBRICKS, POWERUP_RED_ADDRESS, POWERUP_BLUE_ADDRESS, POWERUP_GREEN_ADDRESS, POWERUP_YELLOW_ADDRESS);
                        Opponents[1] = new Opponent(memoryManager, DRIVER_BASE, ENEMY_2_BASE_OFFSETS, DRIVER_OFFSET_COORDINATE_X, DRIVER_OFFSET_COORDINATE_Y, DRIVER_OFFSET_COORDINATE_Z, DRIVER_OFFSET_SPEED_X, DRIVER_OFFSET_SPEED_Y, DRIVER_OFFSET_SPEED_Z, DRIVER_OFFSET_VECTOR_X1, DRIVER_OFFSET_VECTOR_Y1, DRIVER_OFFSET_VECTOR_Z1, DRIVER_OFFSET_VECTOR_X2, DRIVER_OFFSET_VECTOR_Y2, DRIVER_OFFSET_VECTOR_Z2, DRIVER_OFFSET_BRICK, DRIVER_OFFSET_WHITEBRICKS, POWERUP_RED_ADDRESS, POWERUP_BLUE_ADDRESS, POWERUP_GREEN_ADDRESS, POWERUP_YELLOW_ADDRESS);
                        Opponents[2] = new Opponent(memoryManager, DRIVER_BASE, ENEMY_3_BASE_OFFSETS, DRIVER_OFFSET_COORDINATE_X, DRIVER_OFFSET_COORDINATE_Y, DRIVER_OFFSET_COORDINATE_Z, DRIVER_OFFSET_SPEED_X, DRIVER_OFFSET_SPEED_Y, DRIVER_OFFSET_SPEED_Z, DRIVER_OFFSET_VECTOR_X1, DRIVER_OFFSET_VECTOR_Y1, DRIVER_OFFSET_VECTOR_Z1, DRIVER_OFFSET_VECTOR_X2, DRIVER_OFFSET_VECTOR_Y2, DRIVER_OFFSET_VECTOR_Z2, DRIVER_OFFSET_BRICK, DRIVER_OFFSET_WHITEBRICKS, POWERUP_RED_ADDRESS, POWERUP_BLUE_ADDRESS, POWERUP_GREEN_ADDRESS, POWERUP_YELLOW_ADDRESS);
                        Opponents[3] = new Opponent(memoryManager, DRIVER_BASE, ENEMY_4_BASE_OFFSETS, DRIVER_OFFSET_COORDINATE_X, DRIVER_OFFSET_COORDINATE_Y, DRIVER_OFFSET_COORDINATE_Z, DRIVER_OFFSET_SPEED_X, DRIVER_OFFSET_SPEED_Y, DRIVER_OFFSET_SPEED_Z, DRIVER_OFFSET_VECTOR_X1, DRIVER_OFFSET_VECTOR_Y1, DRIVER_OFFSET_VECTOR_Z1, DRIVER_OFFSET_VECTOR_X2, DRIVER_OFFSET_VECTOR_Y2, DRIVER_OFFSET_VECTOR_Z2, DRIVER_OFFSET_BRICK, DRIVER_OFFSET_WHITEBRICKS, POWERUP_RED_ADDRESS, POWERUP_BLUE_ADDRESS, POWERUP_GREEN_ADDRESS, POWERUP_YELLOW_ADDRESS);
                        Opponents[4] = new Opponent(memoryManager, DRIVER_BASE, ENEMY_5_BASE_OFFSETS, DRIVER_OFFSET_COORDINATE_X, DRIVER_OFFSET_COORDINATE_Y, DRIVER_OFFSET_COORDINATE_Z, DRIVER_OFFSET_SPEED_X, DRIVER_OFFSET_SPEED_Y, DRIVER_OFFSET_SPEED_Z, DRIVER_OFFSET_VECTOR_X1, DRIVER_OFFSET_VECTOR_Y1, DRIVER_OFFSET_VECTOR_Z1, DRIVER_OFFSET_VECTOR_X2, DRIVER_OFFSET_VECTOR_Y2, DRIVER_OFFSET_VECTOR_Z2, DRIVER_OFFSET_BRICK, DRIVER_OFFSET_WHITEBRICKS, POWERUP_RED_ADDRESS, POWERUP_BLUE_ADDRESS, POWERUP_GREEN_ADDRESS, POWERUP_YELLOW_ADDRESS);

                        InitializedType = InitializedType.Both;

                        Initialized(InitializedType.Drivers);
                    }

                    Thread.Sleep((int)Settings.RefreshRate);
                }
            }
            catch (Exception exc)
            {
                Console.WriteLine(exc);
            }
        }

        /// <summary>
        /// Retrieves the current menu.
        /// </summary>
        /// <returns>Returns a Menu containing the current menu.</returns>
        public Menu GetCurrentMenu()
        {
            Menu currentMenu = Menu.MainMenu;

                currentMenu = (Menu)memoryManager.ReadByte(memoryManager.ReadInt(MENU_BASE) + CURRENT_MENU_OFFSET);

            return currentMenu;
        }

        /// <summary>
        /// Navigates to a given menu.
        /// </summary>
        /// <param name="targetmenu">The menu to navigate to.</param>
        public void GotoMenu(Menu targetmenu)
        {
            if (initialized)
            {
                Menu currentMenu = GetCurrentMenu();
                Console.WriteLine(currentMenu + " -> " + targetmenu);

                int offset = 0;
                int ECXbase = memoryManager.ReadInt(memoryManager.ReadInt(MENU_BASE) + TARGETMENU_ECX_OFFSET);
                int ESIbase = memoryManager.ReadInt(memoryManager.ReadInt(MENU_BASE) + TARGETMENU_ESI_OFFSET);

                if (ECXbase != ESIbase) // if a prompt is open
                {
                    switch (currentMenu)
                    {
                        case Menu.Options:
                            if (targetmenu == Menu.DisplayOptions)
                                offset = 0x549C;
                            break;
                        case Menu.Build: // delete racer
                            if (targetmenu == Menu.PromptYes)
                                offset = 0x5B38;
                            else if (targetmenu == Menu.PromptNo)
                                offset = 0x5E28;
                            break;
                        case Menu.CreateDriver: // cancel
                            if (targetmenu == Menu.PromptYes)
                                offset = 0x3FD0;
                            else if (targetmenu == Menu.PromptNo)
                                offset = 0x42C0;
                            break;
                        default:
                            return;
                    }
                }
                else
                {
                    switch (currentMenu)
                    {
                        case Menu.MainMenu:
                            if (targetmenu == Menu.Build)
                                offset = 0x1058;
                            else if (targetmenu == Menu.SingleRace)
                                offset = 0x498;
                            else if (targetmenu == Menu.Options)
                                offset = 0x1348;
                            else if (targetmenu == Menu.TimeAttack)
                                offset = 0xD68;
                            else if (targetmenu == Menu.Circuit)
                                offset = 0x788;
                            else
                                return;
                            break;
                        case Menu.Build:
                            if (targetmenu == Menu.MainMenu)
                                offset = 0x5848;
                            else if (targetmenu == Menu.CreateDriver)
                                offset = 0x40C8;
                            else if (targetmenu == Menu.DeleteRacer) // opens a prompt
                                offset = 0x4999;
                            else if (targetmenu == Menu.EditRacer) // Not finished
                                offset = 0x43B8;
                            else if (targetmenu == Menu.CopyRacer)
                                offset = 0x46A8;
                            else
                                return;
                            break;
                        case Menu.Options:
                            if (targetmenu == Menu.MainMenu)
                                offset = 0x18D8;
                            else if (targetmenu == Menu.ControlsP1)
                                offset = 0xA28;
                            else if (targetmenu == Menu.ControlsP2)
                                offset = 0xD18;
                            else if (targetmenu == Menu.GameOptions)
                                offset = 0x448;
                            else if (targetmenu == Menu.PromptDisplayOptions) // opens a prompt
                                offset = 0x51AC;
                            else if (targetmenu == Menu.Options) // currentmenu==DisplayOptions or GameOptions
                                offset = 0x18D8;
                            else
                                return;
                            break;
                        case Menu.Controls:
                            if (targetmenu == Menu.Options)
                                offset = 0x47C;
                            else
                                return;
                            break;
                        case Menu.SingleRace:
                            if (targetmenu == Menu.MainMenu)
                                offset = 0x1CCC;
                            else if (targetmenu == Menu.ChooseRacer)
                                offset = 0x19DC;
                            else
                                return;
                            break;
                        case Menu.TimeAttack:
                            if (targetmenu == Menu.MainMenu)
                                offset = 0x1CC;
                            else
                                return;
                            break;
                        case Menu.Circuit:
                            if (targetmenu == Menu.MainMenu)
                                offset = 0x1c34;
                            else if (targetmenu == Menu.ChooseRacer)
                                offset = 0x1F24;
                            else
                                return;
                            break;
                        case Menu.CreateDriver:
                            if (targetmenu == Menu.CancelDriver) // opens a prompt
                                offset = 0x39F0;
                            else if (targetmenu == Menu.CreateLicense)
                                offset = 0x3CE0;
                            else
                                return;
                            break;
                        case Menu.CreateLicense:
                            if (targetmenu == Menu.CreateDriver)
                                offset = 0xA88;
                            else if (targetmenu == Menu.BuildCar)
                                offset = 0xD78;
                            else
                                return;
                            break;
                        case Menu.BuildCar:
                            if (targetmenu == Menu.Build)
                                offset = 0x11E4;
                            else if (targetmenu == Menu.CreateLicense)
                                offset = 0x14D4;
                            else
                                return;
                            break;
                        case Menu.ChooseRacer:
                            if (targetmenu == Menu.Circuit || targetmenu == Menu.SingleRace || targetmenu == Menu.TimeAttack)
                                offset = 0x4998;
                            else if (targetmenu == Menu.StartRace)
                                offset = 0x40C8;
                            else
                                return;
                            break;
                        default:
                            return;
                    }
                }

                List<byte> codeToInject = new List<byte>();

                codeToInject.Add(0xB9);
                codeToInject.AddRange(BitConverter.GetBytes(ECXbase)); // mov ecx,neededECX
                codeToInject.Add(0xBE);
                codeToInject.AddRange(BitConverter.GetBytes(ESIbase + offset)); // mov esi,neededESI
                codeToInject.AddRange(new byte[] { 0x8B, 0x11, 0x56, 0xFF, 0x52, 0x38, 0xC3 }); // mov edx,[ecx] | push esi | call dword ptr [edx+38] | ret
                
                // Write code to the assigned memory and execute it
                memoryManager.WriteBytes((int)memoryManager.NewMemory, codeToInject.ToArray());
                memoryManager.Execute(memoryManager.NewMemory);
            }
        }

        /// <summary>
        /// Selects a race.
        /// </summary>
        /// <param name="circuit">The race circuit number to select.</param>
        /// <param name="race">The race number to select.</param>
        public void SelectRace(int circuit, int race)
        {
            if (initialized)
            {
                memoryManager.WriteInt(memoryManager.CalculatePointer(memoryManager.ReadInt(MENU_BASE), SELECTED_CIRCUIT_OFFSETS), memoryManager.ReadInt(memoryManager.CalculatePointer(memoryManager.ReadInt(MENU_BASE), CIRCUIT_BASE_OFFSETS)) + 100 * circuit);
                memoryManager.WriteInt(memoryManager.CalculatePointer(memoryManager.ReadInt(MENU_BASE), SELECTED_RACE_OFFSETS), race);
            }
        }

        /// <summary>
        /// Setups a new race.
        /// </summary>
        /// <param name="circuit">The race circuit number to setup.</param>
        /// <param name="race">The race number to setup.</param>
        public void SetupRace(int circuit, int race)
        {
            if (initialized)
            {
                if (GetCurrentMenu() != Menu.SingleRace)
                {
                    if (GetCurrentMenu() != Menu.MainMenu)
                    {
                        if (GetCurrentMenu() != Menu.Build && GetCurrentMenu() != Menu.Circuit && GetCurrentMenu() != Menu.TimeAttack && GetCurrentMenu() != Menu.Options)
                        {
                            return;
                        }

                        GotoMenu(Menu.MainMenu);

                        Thread.Sleep(30);
                    }

                    GotoMenu(Menu.SingleRace);

                    Thread.Sleep(30);
                }

                SelectRace(circuit, race);

                Thread.Sleep(30);

                GotoMenu(Menu.ChooseRacer);
            }
        }

        /// <summary>
        /// Setups a new race.
        /// </summary>
        /// <param name="circuit">The circuit to setup.</param>
        public void SetupRace(Circuit circuit)
        {
            SetupRace(circuit, false);
        }

        /// <summary>
        /// Setups a new race.
        /// </summary>
        /// <param name="circuit">The circuit to setup.</param>
        /// <param name="mirror">Determines whether to setup the mirrored version of the circuit.</param>
        public void SetupRace(Circuit circuit, bool mirror)
        {
            if (mirror)
            {
                SetupRace(circuit.MirrorBlockNumber, circuit.MirrorNumber);
            }
            else
            {
                SetupRace(circuit.BlockNumber, circuit.Number);
            }
        }

        /// <summary>
        /// Sets the value whether to load RRB (AI path) files.
        /// </summary>
        /// <param name="value">The value whether to load RRB (AI path) files or not.</param>
        /// <returns>Returns whether the set was successful or not.</returns>
        private bool SetLoadRRB(bool value)
        {
            bool result = true;

            if (initialized)
            {
                if (value == true)
                {
                    result &= memoryManager.WriteBytes(LOAD_RRB_BASE, new byte[] { 0x75, 0x0C }); // JNE +332AD
                    result &= memoryManager.WriteBytes(LOAD_RRB_BASE+4, new byte[] { 0x75, 0x08 }); // JNE +332AD
                    result &= memoryManager.WriteByte(LOAD_RRB_BASE+0xC, 0x74); // JE
                }
                else
                {
                    result &= memoryManager.WriteBytes(LOAD_RRB_BASE, new byte[] { 0x90, 0x90 }); // NOP NOP
                    result &= memoryManager.WriteBytes(LOAD_RRB_BASE + 4, new byte[] { 0x90, 0x90 }); // NOP NOP
                    result &= memoryManager.WriteByte(LOAD_RRB_BASE + 0xC, 0xEB); // JMP
                }
            }

            return result;
        }

        /// <summary>
        /// Sets the value whether AI drivers use Power-ups.
        /// </summary>
        /// <param name="value">The value whether the AI drivers use Power-ups or not.</param>
        /// <returns>Returns whether the set was successful or not.</returns>
        private bool SetAIUsePowerUps(bool value)
        {
            bool result = true;

            if (value)
            {
                result &= memoryManager.WriteBytes(POWERUP_FUNCTION_BASE, new byte[] { 0x8b, 0x86 });
                result &= memoryManager.WriteBytes(POWERUP_FUNCTION_BASE + 2, BitConverter.GetBytes(DRIVER_OFFSET_BRICK)); // mov eax,[esi+00000CCC]
            }
            else
            {
                int targetMem = (int)memoryManager.NewMemory + 0x300;

                List<byte> bytestowrite = new List<byte>();
                bytestowrite.Add(0xE9);
                bytestowrite.AddRange(BitConverter.GetBytes((int)(targetMem - (POWERUP_FUNCTION_BASE + 5)))); // jmp targetmem
                bytestowrite.Add(0x90); // nop
                result &= memoryManager.WriteBytes(POWERUP_FUNCTION_BASE, bytestowrite.ToArray());

                bytestowrite.Clear();
                bytestowrite.AddRange(new byte[] { 0x8b, 0x0d });
                bytestowrite.AddRange(BitConverter.GetBytes(DRIVER_BASE));// mov ecx,[004C67BC]

                for (int i = 0; i < PLAYER_BASE_OFFSETS.Length - 1; i++)
                {
                    bytestowrite.AddRange(new byte[] { 0x8b, 0x89 });
                    bytestowrite.AddRange(BitConverter.GetBytes(PLAYER_BASE_OFFSETS[i]));
                }

                bytestowrite.AddRange(new byte[] { 0x39, 0xf1 });                           // cmp ecx,esi
                bytestowrite.AddRange(new byte[] { 0x0f, 0x84, 0x05, 0x00, 0x00, 0x00 });   // je +5
                bytestowrite.AddRange(new byte[] { 0x8b, 0xce });                           // mov ecx,esi
                bytestowrite.Add(0x5f);                                                     // pop edi
                bytestowrite.Add(0x5e);                                                     // pop esi
                bytestowrite.Add(0xc3);                                                     // ret
                bytestowrite.AddRange(new byte[] { 0x8b, 0x86 });
                bytestowrite.AddRange(BitConverter.GetBytes(DRIVER_OFFSET_BRICK));   // mov eax,[esi+poweruptype_offset]
                bytestowrite.Add(0xe9);
                bytestowrite.AddRange(BitConverter.GetBytes((POWERUP_FUNCTION_BASE + 6) - (int)(targetMem + bytestowrite.Count + 4)));// jmp 0043910A
                result &= memoryManager.WriteBytes(targetMem, bytestowrite.ToArray());
            }

            return result;
        }

        public bool RemoveMenuButtons()
        {
            bool result = true;
            result &= memoryManager.WriteByte(0x00480F58, 0x00); // build
            result &= memoryManager.WriteByte(0x00480F6C, 0x00); // circuit
            result &= memoryManager.WriteByte(0x00480F80, 0x00); // singlerace
            result &= memoryManager.WriteByte(0x00480F94, 0x55); // versus (moving to location 55 (circuit))
            result &= memoryManager.WriteByte(0x00480FA8, 0x00); // timeattack
            result &= memoryManager.WriteByte(0x00480FBC, 0x00); // options
            //result &= writeByte(0x00480FD0, 0x00); // exit

            result &= memoryManager.WriteByte(0x00484E31, 0x00); // cancel racer selection

            result &= memoryManager.WriteBytes(0x00480FE5, new byte[] { 0x90, 0x90, 0x90, 0x90 });// always disable versus

            result &= memoryManager.WriteByte(0x00480F8A, 46); // set versus button text to line 47 of menustrings.srf
            result &= memoryManager.WriteBytes(GetMenuStringsAddress(46), memoryManager.GetStringBytes("WAITING FOR SERVER TO START A RACE..."));

            return result;
        }

        private int GetMenuStringsAddress(int line)
        {
            // code from 0x0044E500
            int ecx = memoryManager.ReadInt(memoryManager.ReadInt(memoryManager.ReadInt(memoryManager.ReadInt(0x004c4918) + 0x4dc8) + 0x354) + 0x4d00);
            short offset = memoryManager.ReadShort(ecx + (int)line * 2);
            int filestart = memoryManager.ReadInt(memoryManager.ReadInt(memoryManager.ReadInt(memoryManager.ReadInt(0x004c4918) + 0x4dc8) + 0x354) + 0x4cfc);
            return (int)(filestart + offset * 2);
        }
    }
}