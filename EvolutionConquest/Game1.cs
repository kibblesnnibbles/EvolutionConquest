﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

namespace EvolutionConquest
{
    public class Game1 : Game
    {
        //Framework variables
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;
        private InputState _inputState;
        private Player _player;
        private SpriteFont _diagFont;
        private SpriteFont _panelHeaderFont;
        private SpriteFont _foodFont;
        private DatabaseConnectionSettings _dbConnectionSettings;
        private ConnectionManager _connectionManager;
        private int _diagTextHeight;
        private int _frames;
        private int _fps;
        private double _elapsedSeconds;
        private double _totalElapsedSeconds;
        private TimeSpan _resetTimeSpan;
        //Game variables
        private GameData _gameData;
        private Texture2D _whitePixel;
        private Texture2D _basicCreatureTexture;
        private Texture2D _foodTexture;
        private Texture2D _eggTexture;
        private Random _rand;
        private int _gameRandSeed;
        private int _sessionID;
        private CreatureShapeGenerator _creatureGenerator;
        private FoodShapeGenerator _foodGenerator;
        private EggShapeGenerator _eggGenerator;
        private Names _names;
        private Borders _borders;
        private List<string> _creatureStats;
        private int _uniqueSpeciesCount;
        private double _elapsedSecondsSinceTick;
        private double _elapsedTimeSinceFoodGeneration;
        private float _currentTicksPerSecond = 30;
        private float _tickSeconds;
        private float _elapsedTicksSinceSecondProcessing;
        private int _speciesIdCounter;
        private Chart _chart;
        private List<string> _controlsListText;
        private int _creatureIdCtr;
        private int _elapsedTicksSinceFoodUpgrade;
        private int _maxFoodLevel;
        private int _climateHeight;
        private bool _writeStats;
        //Constants
        private const int SESSION_NUMBER = 14;
        private const float SPRITE_FONT_SCALE = 0.5f;
        private const float TICKS_PER_SECOND = 30;
        private const int BORDER_WIDTH = 10;
        private const int GRID_CELL_SIZE = 50; //Seems to be the sweet spot for a 5,000 x 5,000 map based on the texture sizes we have so far
        private const float INIT_FOOD_RATIO = 0.0005f;
        private const float INIT_STARTING_CREATURE_RATIO = 0.0001f;
        //private const float INIT_FOOD_RATIO = 0.01f; //Performance test value
        //private const float INIT_STARTING_CREATURE_RATIO = 0.0005f; //Performance test value
        private const float FOOD_GENERATION_INTERVAL_SECONDS = 0.01f;
        private const int TICKS_TILL_FOOD_UPGRADE = 9000;
        private const int FOOD_UPGRADE_AMOUNT = 1;
        private const int FOOD_UPGRADE_CHANCE_PERCENT = 20; //Between 0-100
        private const int MAX_FOOD_LEVEL = 50;
        private const float FOOD_ENERGY_AMOUNT = 100;
        private const float MOVEMENT_ENERGY_DEPLETION = 0.01f;
        private const float EGG_ENERGY_LOSS = 50;
        //GamePlay feature toggles
        private const bool ENABLE_ENERGY_DEATH = true;
        private const bool ENABLE_SIGHT = true;
        private const bool ENABLE_CLIMATE = true;
        //Colors
        private Color MAP_COLOR = Color.SandyBrown;

        public Game1()
        {
            _graphics = new GraphicsDeviceManager(this);
            _graphics.PreferredBackBufferHeight = 900;
            _graphics.PreferredBackBufferWidth = 1600;

            InitVariables();
            _resetTimeSpan = new TimeSpan(); //This must be initialized outside of the InitVariables function so that it doest not get reset
            _writeStats = true;

            IsMouseVisible = true;

            Content.RootDirectory = "Content";
        }
        protected override void Initialize()
        {
            Global.Camera.ViewportWidth = _graphics.GraphicsDevice.Viewport.Width;
            Global.Camera.ViewportHeight = _graphics.GraphicsDevice.Viewport.Height;
            Global.Camera.CenterOn(new Vector2(Global.Camera.ViewportWidth / 2, Global.Camera.ViewportHeight / 2));

            base.Initialize();
        }
        protected override void LoadContent()
        {
            // Create a new SpriteBatch, which can be used to draw textures.
            _spriteBatch = new SpriteBatch(GraphicsDevice);

            _foodFont = Content.Load<SpriteFont>("FoodFont");
            _panelHeaderFont = Content.Load<SpriteFont>("ArialBlack");
            _diagFont = Content.Load<SpriteFont>("DiagnosticsFont");
            _diagTextHeight = (int)Math.Ceiling(_diagFont.MeasureString("ABCDEFGHIJKLMNOPQRSTUVWXYZ[]").Y);
            _tickSeconds = TICKS_PER_SECOND;

            _whitePixel = new Texture2D(_graphics.GraphicsDevice, 1, 1);
            Color[] color = new Color[1];
            color[0] = Color.White;
            _whitePixel.SetData(color);

            _gameData = new GameData();
            _rand = new Random();
            _sessionID = SESSION_NUMBER;
            //_sessionID = _rand.Next(0, int.MaxValue); //We could have just used the Seed since it is random but I might implement the ability to choose seed at a later time
            _rand = new Random(1);
            _gameRandSeed = _rand.Next(0, int.MaxValue); //Use the initial random class to set a seed
            _rand = new Random(_gameRandSeed); //Re-instantiate the _rand variable with our seed
            _names = new Names();
            _creatureStats = new List<string>();
            _creatureGenerator = new CreatureShapeGenerator();
            _foodGenerator = new FoodShapeGenerator();
            _eggGenerator = new EggShapeGenerator();
            _basicCreatureTexture = _creatureGenerator.CreateCreatureTexture(_graphics.GraphicsDevice);
            _foodTexture = _foodGenerator.CreateFoodTexture(_graphics.GraphicsDevice);
            _eggTexture = _eggGenerator.CreateEggTexture(_graphics.GraphicsDevice, Color.Black, Color.White);

            //Generate the Map
            _borders = new Borders();
            _borders.Texture = _whitePixel;
            _borders.LeftWall = new Vector2(0, 0);
            _borders.RightWall = new Vector2(Global.WORLD_SIZE, 0);
            _borders.TopWall = new Vector2(0, 0);
            _borders.BottomWall = new Vector2(0, Global.WORLD_SIZE);

            //Initialize the Grid
            int gridWidth = (int)Math.Ceiling((double)Global.WORLD_SIZE / GRID_CELL_SIZE);

            _gameData.MapGridData = new GridData[gridWidth, gridWidth];

            //Loop through grid and set Rectangle on each cell, named iterators x,y to help avoid confusion
            for (int y = 0; y < _gameData.MapGridData.GetLength(0); y++)
            {
                for (int x = 0; x < _gameData.MapGridData.GetLength(1); x++)
                {
                    _gameData.MapGridData[x, y] = new GridData();
                    _gameData.MapGridData[x, y].Creatures = new List<Creature>();
                    _gameData.MapGridData[x, y].Eggs = new List<Egg>();
                    _gameData.MapGridData[x, y].Food = new List<Food>();

                    Rectangle rec = new Rectangle();
                    rec.X = x * GRID_CELL_SIZE;
                    rec.Y = y * GRID_CELL_SIZE;
                    rec.Width = GRID_CELL_SIZE;
                    rec.Height = GRID_CELL_SIZE;

                    _gameData.MapGridData[x, y].CellRectangle = rec;
                }
            }

            //Load in random food
            int amountOfFood = (int)(((Global.WORLD_SIZE * Global.WORLD_SIZE) / _foodTexture.Width) * INIT_FOOD_RATIO);
            for (int i = 0; i < amountOfFood; i++)
            {
                SpawnFood();
            }

            //Game start, load in starting population of creatures
            int startingCreatureAmount = (int)(((Global.WORLD_SIZE * Global.WORLD_SIZE) / ((_basicCreatureTexture.Width + _basicCreatureTexture.Height) / 2)) * INIT_STARTING_CREATURE_RATIO);
            for (int i = 0; i < startingCreatureAmount; i++)
            {
                SpawnStartingCreature();
            }
            Creature creature = new Creature();
            creature.InitNewCreature(_rand, ref _names, _speciesIdCounter, ref _creatureIdCtr);
            _speciesIdCounter++;
            creature.Texture = _basicCreatureTexture;
            creature.Position = new Vector2(500, 388);
            creature.Rotation = MathHelper.ToRadians(10);
            creature.ColdClimateTolerance = 6;
            creature.HotClimateTolerance = 0;
            _gameData.Creatures.Add(creature);

            Global.Camera.AdjustZoom(500);
            _gameData.Focus = creature;
            _gameData.FocusIndex = _gameData.Creatures.Count - 1;

            //Game Controls list for HUD
            _controlsListText = new List<string>();
            //Controls: [W][A][S][D] Camera Pan, [PageUp][PageDown] Iterate Creatures, [Shift] + [PageUp][PageDown] Iterate Species, [F12] Toggle Chart";
            _controlsListText.Add("[W][A][S][D] Camera Pan");
            _controlsListText.Add("[Scroll Wheel] Zoom");
            _controlsListText.Add("[PageUp][PageDown] Cycle Creatures");
            _controlsListText.Add("[Shift] + [PageUp][PageDown] Cycle Species");
            _controlsListText.Add("[Ctrl] + [PageUp][PageDown] Cycle Creatures");
            _controlsListText.Add("[F9] Toggle Food Strength");
            _controlsListText.Add("[F10] Toggle Creature Statistics");
            _controlsListText.Add("[F11] Toggle Chart");
            _controlsListText.Add("[Left Click] Follow/Unfollow Creature");
            _controlsListText.Add("[H] Highlight Species");
            _controlsListText.Add(" ");
            _controlsListText.Add("[F12] Toggle Control Menu");

            //Create the chart
            _chart = new Chart();
            _chart.Width = 600;
            _chart.Height = 300;
            _chart.Location = new System.Drawing.Point(_graphics.PreferredBackBufferWidth - _chart.Width - 2, _graphics.PreferredBackBufferHeight - _chart.Height - 2);
            _chart.Text = "Test";
            _chart.Visible = false;
            ChartArea chartArea1 = new ChartArea();
            chartArea1.Name = "ChartArea1";
            _chart.ChartAreas.Add(chartArea1);
            Legend legend = new Legend();
            _chart.Legends.Add(legend);

            Control.FromHandle(Window.Handle).Controls.Add(_chart);

            _dbConnectionSettings = SettingsHelper.ReadSettings("Settings.json");

            //Establish a connection to the SQL Server database, we do not want to run a whole simulation only to find out the SQL Server database is not accessible
            SqlConnection tmpConnection = _connectionManager.GetSqlConnection(_dbConnectionSettings);
            tmpConnection.Close(); //Close the connection right away so that our connection does not timeout while the simulation/game runs
        }
        protected override void UnloadContent()
        {
            // TODO: Unload any non ContentManager content here
        }
        protected override void Update(GameTime gameTime)
        {
            bool tick = false;

            UpdateHandleInputs(gameTime);

            if ((gameTime.TotalGameTime - _resetTimeSpan).TotalMinutes < 60)
            {
                _tickSeconds = 1 / _currentTicksPerSecond;

                _elapsedSecondsSinceTick += gameTime.ElapsedGameTime.TotalSeconds;
                _elapsedTimeSinceFoodGeneration += gameTime.ElapsedGameTime.TotalSeconds;
                if (_elapsedSecondsSinceTick > _tickSeconds)
                {
                    _elapsedSecondsSinceTick = _elapsedSecondsSinceTick - _tickSeconds; //Start the next tick with the overage
                    tick = true;
                }

                //During a tick do all creature processing
                if (tick)
                {
                    UpdateTick(gameTime);
                }
                else //Off tick processing
                {
                    UpdateOffTick(gameTime);
                }
            }
            else
            {
                //Simulation has ended. Time to write stats to the database
                UpdateHandleEndOfSimulation(gameTime);
                _writeStats = false;
            }

            //This must be after movement caluclations occur for the creatures otherwise the camera will glitch back and forth
            if (_gameData.Focus != null)
            {
                Global.Camera.CenterOn(_gameData.Focus.Position);
            }

            base.Update(gameTime);
        }
        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(MAP_COLOR);

            //FPS Counter
            _totalElapsedSeconds += gameTime.ElapsedGameTime.TotalSeconds;
            if (_elapsedSeconds >= 1)
            {
                _fps = _frames;
                _frames = 0;
                _elapsedSeconds = 0;
            }
            _frames++;
            _elapsedSeconds += gameTime.ElapsedGameTime.TotalSeconds;

            if (_fps >= 30)
                if (1 == 1)
                { }

            // === DRAW WITHIN THE WORLD ===
            DrawWorldObjects();
            //=== DRAW HUD INFORMATION, DOES NOT DRAW TO WORLD SCALE ===
            DrawHUD(); 

            base.Draw(gameTime);
        }

        //Update functions
        private void UpdateTick(GameTime gameTime)
        {
            _elapsedTicksSinceSecondProcessing++;

            //Check eggs before creatures so that the baby can follow cretaure update logic when born such as eating food
            UpdateTickEggHatching(gameTime);

            //Creature logic
            UpdateTickCreature(gameTime);

            //Food logic
            UpdateTickFood(gameTime);
        }
        private void UpdateTickEggHatching(GameTime gameTime)
        {
            for (int i = _gameData.Eggs.Count - 1; i >= 0; i--)
            {
                _gameData.Eggs[i].AdvanceTick();
                //Check to egg hatched
                if (_gameData.Eggs[i].ElapsedTicks >= _gameData.Eggs[i].TicksTillHatched)
                {
                    //TODO change texture based on creature properties dynamically
                    //Assign texture to the creature
                    _gameData.Eggs[i].Creature.Texture = _basicCreatureTexture;
                    _gameData.Creatures.Add(_gameData.Eggs[i].Creature);
                    _gameData.RemoveEggFromGrid(_gameData.Eggs[i], _gameData.Eggs[i].GridPositions);
                    _gameData.Eggs.RemoveAt(i);
                }
            }
        }
        private void UpdateTickCreature(GameTime gameTime)
        {
            for (int i = _gameData.Creatures.Count - 1; i >= 0; i--)
            {
                _gameData.Creatures[i].AdvanceTick(_rand);

                //Check if the creature has died
                if (_gameData.Creatures[i].ElapsedTicks > _gameData.Creatures[i].Lifespan || (ENABLE_ENERGY_DEATH && _gameData.Creatures[i].Energy < 0))
                {
                    _gameData.Creatures[i].IsAlive = false;
                    if (ENABLE_ENERGY_DEATH && _gameData.Creatures[i].Energy < 0)
                    {
                        _gameData.Creatures[i].DeathCause = "Energy";
                    }
                    else
                    {
                        _gameData.Creatures[i].DeathCause = "Age";
                    }
                    _gameData.DeadCreatures.Add(_gameData.Creatures[i]);
                    
                    //Drop all food on the ground randomly around the area
                    for (int k = 0; k < _gameData.Creatures[i].UndigestedFood; k++)
                    {
                        SpawnFood(new Vector2(_gameData.Creatures[i].Position.X + _rand.Next(-15, 15), _gameData.Creatures[i].Position.Y + _rand.Next(-15, 15)), _gameData.Creatures[i].Herbavore);
                    }
                    if (_gameData.Focus == _gameData.Creatures[i])
                    {
                        _gameData.Focus = null;
                        _gameData.FocusIndex = -1;
                    }
                    _gameData.RemoveCreatureFromGrid(_gameData.Creatures[i], _gameData.Creatures[i].GridPositions);
                    _gameData.Creatures.RemoveAt(i);
                    continue;
                }
                //Check if we can lay a new egg
                if (_gameData.Creatures[i].DigestedFood > 0 && _gameData.Creatures[i].TicksSinceLastEgg >= _gameData.Creatures[i].EggInterval)
                {
                    _gameData.Creatures[i].DigestedFood--; //Costs one digested food to lay an egg
                    Egg egg = _gameData.Creatures[i].LayEgg(_rand, ref _names, _gameData.Creatures, ref _creatureIdCtr);
                    //TODO handle this maybe in the Creature class
                    egg.Texture = _eggTexture;
                    egg.GridPositions = GetGridPositionsForSpriteBase(egg);
                    _gameData.AddEggToGrid(egg);
                    _gameData.Eggs.Add(egg); //Add the new egg to gameData, the LayEgg function will calculate the Mutations
                    _gameData.Creatures[i].Energy -= EGG_ENERGY_LOSS;
                }
                //Do vision checks
                if (ENABLE_SIGHT)
                {
                    //Vision is calculated from the center of the creature, if they do not have vision greater than their texture size there is no point calculating for vision
                    if (_gameData.Creatures[i].Sight > _gameData.Creatures[i].TextureCollideDistance)
                    {
                        bool foundFood = false;
                        Vector2 newDestination = Vector2.Zero;
                        float newDestinationDistance = 99999999999999f;

                        foundFood = CheckForNearestFoodLocation(_gameData.Creatures[i].GridPositions, _gameData.Creatures[i], ref newDestination, ref newDestinationDistance);

                        //Need to expand the search area if we did not find an object. This will be more noticable as the creature sight increases
                        if (!foundFood)
                        {
                            List<Point> gridLocations = new List<Point>();
                            foreach (Point p in _gameData.Creatures[i].GridPositions)
                            {
                                if (p.X > 0)
                                {
                                    gridLocations.Add(new Point(p.X - 1, p.Y));

                                    if (p.Y > 0)
                                    {
                                        gridLocations.Add(new Point(p.X - 1, p.Y - 1));
                                    }
                                    if (p.Y < _gameData.MapGridData.GetLength(1) - 1)
                                    {
                                        gridLocations.Add(new Point(p.X - 1, p.Y + 1));
                                    }
                                }
                                if (p.X < _gameData.MapGridData.GetLength(0) - 1)
                                {
                                    gridLocations.Add(new Point(p.X + 1, p.Y));

                                    if (p.Y > 0)
                                    {
                                        gridLocations.Add(new Point(p.X + 1, p.Y - 1));
                                    }
                                    if (p.Y < _gameData.MapGridData.GetLength(1) - 1)
                                    {
                                        gridLocations.Add(new Point(p.X + 1, p.Y + 1));
                                    }
                                }
                                if (p.Y > 0)
                                {
                                    gridLocations.Add(new Point(p.X, p.Y - 1));
                                }
                                if (p.Y < _gameData.MapGridData.GetLength(1) - 1)
                                {
                                    gridLocations.Add(new Point(p.X, p.Y + 1));
                                }
                            }

                            //Now that we have a list of grid locations we need to remove the ones we just checked
                            for (int j = gridLocations.Count - 1; j >= 0; j--)
                            {
                                foreach (Point p in _gameData.Creatures[i].GridPositions)
                                {
                                    if (gridLocations[j].X == p.X && gridLocations[j].Y == p.Y)
                                    {
                                        gridLocations.RemoveAt(j);
                                    }
                                }
                            }

                            //Check expanded
                            foundFood = CheckForNearestFoodLocation(gridLocations, _gameData.Creatures[i], ref newDestination, ref newDestinationDistance);
                        }

                        if (foundFood)
                        {
                            //TODO Calculate the Intercept angle for the Food
                        }
                    }
                }
            }
        }
        private void UpdateTickFood(GameTime gameTime)
        {
            if (_maxFoodLevel < MAX_FOOD_LEVEL)
            {
                _elapsedTicksSinceFoodUpgrade++;
                if (_elapsedTicksSinceFoodUpgrade >= TICKS_TILL_FOOD_UPGRADE)
                {
                    //Chance to increase the food level
                    if (_rand.Next(0, 100) <= FOOD_UPGRADE_CHANCE_PERCENT)
                    {
                        _elapsedTicksSinceFoodUpgrade = 0;
                        _maxFoodLevel += FOOD_UPGRADE_AMOUNT;
                    }
                }
            }
        }
        private void UpdateOffTick(GameTime gameTime)
        {
            //Spawn new food
            UpdateOffTickSpawnFood(gameTime);
            //Collisions And Movement
            UpdateOffTickHandleCollisionsAndMovement(gameTime);

            //Every second interval processing only when it is not a TICK. Graph only needs to be updated once every X seconds
            if (_elapsedTicksSinceSecondProcessing >= TICKS_PER_SECOND * 5)
            {
                UpdateOffTickInterval(gameTime);
            }

            //Display chart logic here as well so that we do not need to wait 5 seconds
            if (_gameData.ChartDataTop.Count > 0)
            {
                _chart.Visible = _gameData.ShowChart;
            }
        }
        private void UpdateOffTickInterval(GameTime gameTime)
        {
            _elapsedTicksSinceSecondProcessing = 0;

            //For the HUD calculate the unique number of species. Do this in the "Per Second" code so that it is not a big performance hit
            _uniqueSpeciesCount = _gameData.GetUniqueSpeciesCount();

            //Generate Graph data
            _gameData.CalculateChartData(); //This will populat the Chart Data in _gameData. Even if we hide the chart we need to keep track of ChartData

            if (_gameData.ShowChart)
            {
                if (!_chart.Visible && _gameData.ChartDataTop.Count > 0)
                {
                    _chart.Visible = true;
                }

                _chart.Series.Clear();
                for (int i = 0; i < _gameData.ChartDataTop.Count; i++)
                {
                    int? count = _gameData.ChartDataTop[i].CountsOverTime[_gameData.ChartDataTop[i].CountsOverTime.Count - 1];
                    string name = String.Empty;
                    if (count != null)
                    {
                        name = _gameData.ChartDataTop[i].Name + "(" + count + ")";
                    }
                    else
                    {
                        name = _gameData.ChartDataTop[i].Name;
                    }

                    _chart.Series.Add(name);
                    _chart.Series[name].XValueType = ChartValueType.Int32;
                    _chart.Series[name].ChartType = SeriesChartType.StackedArea100;
                    _chart.Series[name].BorderWidth = 3;
                    for (int k = 0; k < _gameData.ChartDataTop[i].CountsOverTime.Count; k++)
                    {
                        _chart.Series[name].Points.AddXY(k, _gameData.ChartDataTop[i].CountsOverTime[k]);
                    }
                }
            }
        }
        private void UpdateOffTickHandleCollisionsAndMovement(GameTime gameTime)
        {
            //CollisionDetection
            //Border Collision Detection
            for (int i = 0; i < _gameData.Creatures.Count; i++)
            {
                if (!_gameData.Creatures[i].IsLeavingClimate)
                {
                    if (_gameData.Creatures[i].Position.X - (_gameData.Creatures[i].Texture.Width / 2) <= 0 || _gameData.Creatures[i].Position.X + (_gameData.Creatures[i].Texture.Width / 2) >= Global.WORLD_SIZE)
                    {
                        if (_gameData.Creatures[i].Direction.X >= 0 && _gameData.Creatures[i].Direction.Y >= 0 ||
                            _gameData.Creatures[i].Direction.X >= 0 && _gameData.Creatures[i].Direction.Y < 0 ||
                            _gameData.Creatures[i].Direction.X < 0 && _gameData.Creatures[i].Direction.Y >= 0 ||
                            _gameData.Creatures[i].Direction.X < 0 && _gameData.Creatures[i].Direction.Y < 0)
                        {
                            _gameData.Creatures[i].Rotation = (((float)Math.PI * 2) - _gameData.Creatures[i].Rotation);
                        }
                    }
                    if (_gameData.Creatures[i].Position.Y - (_gameData.Creatures[i].Texture.Height / 2) <= 0 || _gameData.Creatures[i].Position.Y + (_gameData.Creatures[i].Texture.Height / 2) >= Global.WORLD_SIZE)
                    {
                        if (_gameData.Creatures[i].Direction.X >= 0 && _gameData.Creatures[i].Direction.Y >= 0 ||
                            _gameData.Creatures[i].Direction.X >= 0 && _gameData.Creatures[i].Direction.Y < 0 ||
                            _gameData.Creatures[i].Direction.X < 0 && _gameData.Creatures[i].Direction.Y >= 0 ||
                            _gameData.Creatures[i].Direction.X < 0 && _gameData.Creatures[i].Direction.Y < 0)
                        {
                            _gameData.Creatures[i].Rotation = (((float)Math.PI) - _gameData.Creatures[i].Rotation);
                        }
                    }
                }

                //Food collision
                if (_gameData.Creatures[i].IsHerbavore)
                {
                    foreach (Point p in _gameData.Creatures[i].GridPositions)
                    {
                        for (int k = (_gameData.MapGridData[p.X, p.Y].Food.Count - 1); k >= 0; k--)
                        {
                            if (_gameData.Creatures[i].Herbavore >= _gameData.MapGridData[p.X, p.Y].Food[k].FoodStrength)
                            {
                                if (_gameData.Creatures[i].Bounds.Intersects(_gameData.MapGridData[p.X, p.Y].Food[k].Bounds))
                                {
                                    Food tmpFood = _gameData.MapGridData[p.X, p.Y].Food[k];
                                    _gameData.Creatures[i].UndigestedFood++;
                                    _gameData.Creatures[i].TotalFoodEaten++;
                                    _gameData.Creatures[i].Energy += FOOD_ENERGY_AMOUNT + (_gameData.Creatures[i].FoodDigestion / 10); //Slower food digestion means you pull more energy from the food
                                    _gameData.RemoveFoodFromGrid(tmpFood, _gameData.MapGridData[p.X, p.Y].Food[k].GridPositions);
                                    _gameData.Food.Remove(tmpFood);
                                }
                            }
                        }
                    }
                }

                if (_gameData.Creatures[i].IsAlive)
                {
                    //Move the creature
                    _gameData.Creatures[i].Position += _gameData.Creatures[i].Direction * ((_gameData.Creatures[i].Speed / 10f) * (_currentTicksPerSecond / TICKS_PER_SECOND)) * TICKS_PER_SECOND * (float)gameTime.ElapsedGameTime.TotalSeconds;
                    _gameData.Creatures[i].GridPositions = GetGridPositionsForSpriteBase(_gameData.Creatures[i]);
                    _gameData.Creatures[i].Energy -= _gameData.Creatures[i].Speed * MOVEMENT_ENERGY_DEPLETION;

                    if (_gameData.Creatures[i].CurrentGridPositionsForCompare != _gameData.Creatures[i].OldGridPositionsForCompare)
                    {
                        List<Point> delta = _gameData.Creatures[i].GetGridDelta();
                        if (delta.Count > 0)
                        {
                            _gameData.RemoveCreatureFromGrid(_gameData.Creatures[i], delta);
                        }
                    }
                }
            }
        }
        private void UpdateOffTickSpawnFood(GameTime gameTime)
        {
            //Spawn new food
            if (_elapsedTimeSinceFoodGeneration > FOOD_GENERATION_INTERVAL_SECONDS)
            {
                _elapsedTimeSinceFoodGeneration -= FOOD_GENERATION_INTERVAL_SECONDS;
                SpawnFood();
            }
        }
        private void UpdateHandleInputs(GameTime gameTime)
        {
            if (_inputState.IsExitGame(PlayerIndex.One))
            {
                Exit();
            }
            else
            {
                _inputState.Update();
                _player.HandleInput(_inputState, PlayerIndex.One, ref _gameData);
                Global.Camera.HandleInput(_inputState, PlayerIndex.One, ref _gameData);
            }
        }
        private void UpdateHandleEndOfSimulation(GameTime gameTime)
        {
            if (_writeStats)
            {
                SqlConnection connection = _connectionManager.GetSqlConnection(_dbConnectionSettings);

                //Insert all of the Live creatures first
                for (int i = 0; i < _gameData.Creatures.Count; i++)
                {
                    List<string> sqlStatements = _gameData.Creatures[i].GetCreatureStatisticsSQL(_gameRandSeed, _sessionID, INIT_STARTING_CREATURE_RATIO, INIT_FOOD_RATIO, (gameTime.TotalGameTime - _resetTimeSpan).TotalMinutes);
                    foreach (string s in sqlStatements)
                    {
                        SqlCommand cmd = _connectionManager.GetSqlCommand(connection, s);
                        cmd.ExecuteNonQuery();
                    }
                }

                //Insert all the dead creatures
                for (int i = 0; i < _gameData.DeadCreatures.Count; i++)
                {
                    List<string> sqlStatements = _gameData.DeadCreatures[i].GetCreatureStatisticsSQL(_gameRandSeed, _sessionID, INIT_STARTING_CREATURE_RATIO, INIT_FOOD_RATIO, (gameTime.TotalGameTime - _resetTimeSpan).TotalMinutes);
                    foreach (string s in sqlStatements)
                    {
                        SqlCommand cmd = _connectionManager.GetSqlCommand(connection, s);
                        cmd.ExecuteNonQuery();
                    }
                }

                ResetGame(gameTime);
            }
            else
            {
                //Reset the timespan so that we continue to run. Disable the warning for now, we will probably move a bunch of settings to the config later
                #pragma warning disable CS0162 // Unreachable code detected
                _resetTimeSpan = gameTime.TotalGameTime;
                #pragma warning restore CS0162 // Unreachable code detected
            }
        }

        //Draw World Functions
        private void DrawWorldObjects()
        {
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, null, null, null, null, Global.Camera.TranslationMatrix);

            DrawClimates();
            DrawFood();
            DrawEggs();
            DrawCreatures();
            DrawBorders();
            DrawHighlightCreatures();

            _spriteBatch.End();
        }
        private void DrawClimates()
        {
            //_spriteBatch.Draw(_borders.Texture, new Rectangle((int)_borders.LeftWall.X - BORDER_WIDTH, (int)_borders.LeftWall.Y, BORDER_WIDTH, Global.WORLD_SIZE + BORDER_WIDTH), Color.SaddleBrown);
            _spriteBatch.Draw(_whitePixel, new Rectangle(0, 0, Global.WORLD_SIZE, _climateHeight), Color.LightSkyBlue);
            _spriteBatch.Draw(_whitePixel, new Rectangle(0, Global.WORLD_SIZE - _climateHeight, Global.WORLD_SIZE, _climateHeight), Color.DarkOrange);
        }
        private void DrawCreatures()
        {
            for (int i = 0; i < _gameData.Creatures.Count; i++)
            {
                if (_gameData.Creatures[i].IsAlive)
                {
                    _spriteBatch.Draw(_gameData.Creatures[i].Texture, _gameData.Creatures[i].Position, null, Color.White, _gameData.Creatures[i].Rotation, _gameData.Creatures[i].Origin, 1f, SpriteEffects.None, 1f);
                }
            }
        }
        private void DrawEggs()
        {
            for (int i = 0; i < _gameData.Eggs.Count; i++)
            {
                _spriteBatch.Draw(_gameData.Eggs[i].Texture, _gameData.Eggs[i].Position, null, Color.White, 0f, _gameData.Eggs[i].Origin, 1f, SpriteEffects.None, 1f);
            }
        }
        private void DrawFood()
        {
            for (int i = 0; i < _gameData.Food.Count; i++)
            {
                _spriteBatch.Draw(_gameData.Food[i].Texture, _gameData.Food[i].Position, null, Color.White, 0f, _gameData.Food[i].Origin, 1f, SpriteEffects.None, 1f);
                if (_gameData.ShowFoodStrength)
                {
                    _spriteBatch.DrawString(_foodFont, _gameData.Food[i].DisplayText, new Vector2(_gameData.Food[i].Position.X, _gameData.Food[i].Position.Y + (_gameData.Food[i].Texture.Height / 2) + (_gameData.Food[i].TextSize.Y / 2)), Color.Black, 0f, new Vector2(_gameData.Food[i].TextSize.X / 2, _gameData.Food[i].TextSize.Y / 2), 1f, SpriteEffects.None, 1f);
                }
            }
        }
        private void DrawBorders()
        {
            _spriteBatch.Draw(_borders.Texture, new Rectangle((int)_borders.LeftWall.X - BORDER_WIDTH, (int)_borders.LeftWall.Y, BORDER_WIDTH, Global.WORLD_SIZE + BORDER_WIDTH), Color.SaddleBrown);
            _spriteBatch.Draw(_borders.Texture, new Rectangle((int)_borders.RightWall.X, (int)_borders.RightWall.Y - BORDER_WIDTH, BORDER_WIDTH, Global.WORLD_SIZE + BORDER_WIDTH), Color.SaddleBrown);
            _spriteBatch.Draw(_borders.Texture, new Rectangle((int)_borders.TopWall.X - BORDER_WIDTH, (int)_borders.TopWall.Y - BORDER_WIDTH, Global.WORLD_SIZE + BORDER_WIDTH, BORDER_WIDTH), Color.SaddleBrown);
            _spriteBatch.Draw(_borders.Texture, new Rectangle((int)_borders.BottomWall.X - BORDER_WIDTH, (int)_borders.BottomWall.Y, Global.WORLD_SIZE + (BORDER_WIDTH * 2), BORDER_WIDTH), Color.SaddleBrown);
        }
        private void DrawHighlightCreatures()
        {
            if (_gameData.HighlightSpecies && _gameData.Focus != null)
            {
                int followedSpecies = _gameData.Focus.SpeciesId;
                int borderWidth = 3;

                for (int i = 0; i < _gameData.Creatures.Count; i++)
                {
                    if (_gameData.Creatures[i].SpeciesId == followedSpecies)
                    {
                        int diagnolLength = (int)Math.Ceiling(Math.Sqrt((_gameData.Creatures[i].Texture.Width * _gameData.Creatures[i].Texture.Width) + (_gameData.Creatures[i].Texture.Height * _gameData.Creatures[i].Texture.Height)));
                        float upperLeftX = _gameData.Creatures[i].Position.X - (diagnolLength / 2), upperLeftY = _gameData.Creatures[i].Position.Y - (diagnolLength / 2);

                        _spriteBatch.Draw(_whitePixel, new Rectangle((int)upperLeftX - borderWidth, (int)upperLeftY - borderWidth, diagnolLength + borderWidth * 2, borderWidth), Color.Red);
                        _spriteBatch.Draw(_whitePixel, new Rectangle((int)upperLeftX - borderWidth, (int)upperLeftY + diagnolLength, diagnolLength + borderWidth * 2, borderWidth), Color.Red);
                        _spriteBatch.Draw(_whitePixel, new Rectangle((int)upperLeftX + diagnolLength, (int)upperLeftY - borderWidth, borderWidth, diagnolLength + borderWidth * 2), Color.Red);
                        _spriteBatch.Draw(_whitePixel, new Rectangle((int)upperLeftX - borderWidth, (int)upperLeftY - borderWidth, borderWidth, diagnolLength + borderWidth * 2), Color.Red);
                    }
                }
            }
        }

        //Draw HUD Functions
        private void DrawHUD()
        {
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, null);

            DrawCreatureStatsPanel();
            DrawMapStatisctics();
            DrawControlsPanel();
            DrawChartBorder();
            DrawFPS();

            _spriteBatch.End();
        }
        private void DrawCreatureStatsPanel()
        {
            if (_gameData.ShowCreatureStats && _gameData.Focus != null)
            {
                DrawPanelWithText(_panelHeaderFont, "Creature Statistics", _diagFont, _gameData.Focus.GetCreatureInformation(), Global.Anchor.LeftCenter, (int)Math.Ceiling(_diagFont.MeasureString("Position: {X:-100.000000, Y:-100.000000}").X), 0, 20);
            }
        }
        private void DrawMapStatisctics()
        {
            string deadCreatures = String.Empty;

            if (_gameData.DeadCreatures.Count >= 1000)
            {
                deadCreatures = Math.Round(_gameData.DeadCreatures.Count / 1000.0, 2).ToString("#,##0.00") + "k";
            }
            else if (_gameData.DeadCreatures.Count >= 1000000)
            {
                deadCreatures = Math.Round(_gameData.DeadCreatures.Count / 1000000.0, 2).ToString("#,##0.00") + "m";
            }
            else
            {
                deadCreatures = _gameData.DeadCreatures.Count.ToString("#,##0");
            }

            string mapStats = "Alive Creatures: " + _gameData.Creatures.Count.ToString("#,##0") + ", Unique Species: " +
                _uniqueSpeciesCount.ToString("#,##0") + ", Dead Creatures: " + deadCreatures + ", Eggs: " +
                _gameData.Eggs.Count.ToString("#,##0") + ", Map Food: " + _gameData.Food.Count.ToString("#,##0");

            Vector2 textSize = _diagFont.MeasureString(mapStats);
            Vector2 drawPos = new Vector2((_graphics.PreferredBackBufferWidth / 2) - (_diagFont.MeasureString(mapStats).X / 2), 10);

            _spriteBatch.Draw(_whitePixel, new Rectangle((int)drawPos.X, (int)drawPos.Y, (int)textSize.X, (int)textSize.Y), Color.White);
            _spriteBatch.DrawString(_diagFont, mapStats, drawPos, Color.Black);//, 0f, Vector2.Zero, SPRITE_FONT_SCALE, SpriteEffects.None, 1f);
        }
        private void DrawControlsPanel()
        {
            if (_gameData.ShowControls)
            {
                DrawPanelWithText(_panelHeaderFont, "Controls", _diagFont, _controlsListText, Global.Anchor.TopRight, 0, 0, 20);
            }
            else
            {
                DrawPanelWithText(_panelHeaderFont, String.Empty, _diagFont, new List<string> { "[F11] Show Controls" }, Global.Anchor.TopRight, 0, 0, 20);
            }
        }
        private void DrawChartBorder()
        {
            if (_gameData.ShowChart && _gameData.ChartDataTop.Count > 0)
            {
                int borderDepth = 2;

                _spriteBatch.Draw(_whitePixel, new Rectangle(_chart.Location.X - borderDepth, _chart.Location.Y - borderDepth, _chart.Width + (borderDepth * 2), _chart.Height + (borderDepth * 2)), Color.Black);
            }
        }
        private void DrawFPS()
        {
            //_spriteBatch.DrawString(_diagFont, "FPS: " + _fps + "   " + (int)_totalElapsedSeconds, new Vector2(5, 5), Color.Black);
            _spriteBatch.DrawString(_diagFont, "FPS: " + _fps, new Vector2(2, 2), Color.Black);
        }
        private void DrawPanelWithText(SpriteFont headerFont, string header, SpriteFont textFont, List<string> text, Global.Anchor anchor, int lockedWidthValue, int lockedHeightValue, int screenBuffer)
        {
            Color borderColor = Color.Black;
            Color headerBackgroundColor = Color.LightGreen;
            Color headerTextColor = Color.DarkGreen;
            Color textColor = Color.DarkGreen;
            Color textBackgroundColor = Color.LightBlue;
            int startingX = 0, startingY = 0, width = 0, height = 0, headerHeight = 0, textHeight = 0, textWidth = 0, headerTextHeight = 0;
            int borderDepth = 2, textSpacing = 5;
            Vector2 headerSize;
            bool drawHeader = true;

            if (String.IsNullOrEmpty(header))
                drawHeader = false;

            if (drawHeader)
            {
                headerSize = headerFont.MeasureString(header);
                headerTextHeight = (int)Math.Ceiling(headerSize.Y);
                textWidth = (int)Math.Ceiling(headerSize.Y);
            }

            if (lockedWidthValue == 0)
            {
                foreach (string s in text)
                {
                    int tmpSize = (int)Math.Ceiling(textFont.MeasureString(s).X);
                    if (tmpSize > textWidth)
                    {
                        textWidth = tmpSize;
                    }
                }
            }
            else
            {
                textWidth = lockedWidthValue;
            }

            if (lockedHeightValue == 0)
            {
                textHeight = _diagTextHeight;
            }
            else
            {
                textHeight = lockedHeightValue;
            }

            if (drawHeader)
                headerHeight = (textSpacing * 4) + headerTextHeight;
            height = headerHeight + (borderDepth * 2) + (text.Count * (textHeight + textSpacing)) + textSpacing;
            width = (borderDepth * 2) + textWidth + (textSpacing * 2);

            switch (anchor)
            {
                case Global.Anchor.TopCenter:
                    startingX = (_graphics.PreferredBackBufferWidth / 2) - (width / 2);
                    startingY = screenBuffer;
                    break;
                case Global.Anchor.BottomCenter:
                    startingX = (_graphics.PreferredBackBufferWidth / 2) - (width / 2);
                    startingY = _graphics.PreferredBackBufferHeight - height - screenBuffer;
                    break;
                case Global.Anchor.RightCenter:
                    startingX = _graphics.PreferredBackBufferWidth - width - screenBuffer;
                    startingY = (_graphics.PreferredBackBufferHeight / 2) - (height / 2);
                    break;
                case Global.Anchor.LeftCenter:
                    startingX = screenBuffer;
                    startingY = (_graphics.PreferredBackBufferHeight / 2) - (height / 2);
                    break;
                case Global.Anchor.TopLeft:
                    startingX = screenBuffer;
                    startingY = screenBuffer;
                    break;
                case Global.Anchor.TopRight:
                    startingX = _graphics.PreferredBackBufferWidth - width - screenBuffer;
                    startingY = screenBuffer;
                    break;
                case Global.Anchor.BottomLeft:
                    startingX = screenBuffer;
                    startingY = _graphics.PreferredBackBufferHeight - height - screenBuffer;
                    break;
                case Global.Anchor.BottomRight:
                    startingX = _graphics.PreferredBackBufferWidth - width - screenBuffer;
                    startingY = _graphics.PreferredBackBufferHeight - height - screenBuffer;
                    break;
            }

            int currentX = startingX + borderDepth + textSpacing;
            int currentY = startingY + borderDepth + textSpacing;

            _spriteBatch.Draw(_whitePixel, new Rectangle(startingX, startingY, width, height), borderColor);
            _spriteBatch.Draw(_whitePixel, new Rectangle(startingX + borderDepth, startingY + borderDepth, width - borderDepth * 2, height - borderDepth * 2), textBackgroundColor);
            if (drawHeader)
                _spriteBatch.Draw(_whitePixel, new Rectangle(startingX + borderDepth, startingY + borderDepth, width - borderDepth * 2, headerHeight), headerBackgroundColor);

            if (drawHeader)
            {
                currentY += textSpacing;
                _spriteBatch.DrawString(_panelHeaderFont, header, new Vector2(currentX, currentY), headerTextColor);
                currentY += headerTextHeight + (textSpacing * 3);
            }

            for (int i = 0; i < text.Count; i++)
            {
                _spriteBatch.DrawString(textFont, text[i], new Vector2(currentX, currentY), textColor);
                currentY += textHeight + textSpacing;
            }
        }

        //Helper functions
        private void ResetGame(GameTime gameTime)
        {
            _resetTimeSpan = gameTime.TotalGameTime;
            Control.FromHandle(Window.Handle).Controls.Remove(_chart);
            InitVariables();
            LoadContent();
        }
        private void InitVariables()
        {
            _inputState = new InputState();
            _player = new Player();
            _connectionManager = new ConnectionManager();
            _elapsedSecondsSinceTick = 0;
            _elapsedTimeSinceFoodGeneration = 0;
            _elapsedTicksSinceSecondProcessing = 0;
            _speciesIdCounter = 0;
            _fps = 0;
            _frames = 0;
            _elapsedSeconds = 0.0;
            _totalElapsedSeconds = 0.0;
            _creatureIdCtr = 0;
            _elapsedTicksSinceFoodUpgrade = 0;
            _maxFoodLevel = 2;
            _climateHeight = (int)(Global.WORLD_SIZE * (Global.CLIMATE_HEIGHT_PERCENT * 0.01));
        }
        private List<Point> GetGridPositionsForSpriteBase(SpriteBase sb)
        {
            List<Point> gridPositions = new List<Point>();

            //Find the exact grid position we are in then check the surrounding grid locations
            Point exactGridPos = sb.CalculateGridPosition(GRID_CELL_SIZE);
            gridPositions.Add(exactGridPos);

            if (exactGridPos.X > 0 && sb.Bounds.Intersects(_gameData.MapGridData[exactGridPos.X - 1, exactGridPos.Y].CellRectangle))
            {
                gridPositions.Add(new Point(exactGridPos.X - 1, exactGridPos.Y));
                if (exactGridPos.Y > 0 && sb.Bounds.Intersects(_gameData.MapGridData[exactGridPos.X - 1, exactGridPos.Y - 1].CellRectangle))
                {
                    gridPositions.Add(new Point(exactGridPos.X - 1, exactGridPos.Y - 1));
                }
                else if (exactGridPos.Y < _gameData.MapGridData.GetLength(1) - 1 && sb.Bounds.Intersects(_gameData.MapGridData[exactGridPos.X - 1, exactGridPos.Y + 1].CellRectangle))
                {
                    gridPositions.Add(new Point(exactGridPos.X - 1, exactGridPos.Y + 1));
                }
            }
            else if (exactGridPos.X < _gameData.MapGridData.GetLength(0) - 1 && sb.Bounds.Intersects(_gameData.MapGridData[exactGridPos.X + 1, exactGridPos.Y].CellRectangle))
            {
                gridPositions.Add(new Point(exactGridPos.X + 1, exactGridPos.Y));
                if (exactGridPos.Y > 0 && sb.Bounds.Intersects(_gameData.MapGridData[exactGridPos.X + 1, exactGridPos.Y - 1].CellRectangle))
                {
                    gridPositions.Add(new Point(exactGridPos.X + 1, exactGridPos.Y - 1));
                }
                else if (exactGridPos.Y < _gameData.MapGridData.GetLength(1) - 1 && sb.Bounds.Intersects(_gameData.MapGridData[exactGridPos.X + 1, exactGridPos.Y + 1].CellRectangle))
                {
                    gridPositions.Add(new Point(exactGridPos.X + 1, exactGridPos.Y + 1));
                }
            }

            if (exactGridPos.Y > 0 && sb.Bounds.Intersects(_gameData.MapGridData[exactGridPos.X, exactGridPos.Y - 1].CellRectangle))
            {
                gridPositions.Add(new Point(exactGridPos.X, exactGridPos.Y - 1));
            }
            else if (exactGridPos.Y < _gameData.MapGridData.GetLength(1) - 1 && sb.Bounds.Intersects(_gameData.MapGridData[exactGridPos.X, exactGridPos.Y + 1].CellRectangle))
            {
                gridPositions.Add(new Point(exactGridPos.X, exactGridPos.Y + 1));
            }

            //Move the Current Grid position string to the Old
            string gridPosition = String.Empty;
            foreach (Point p in gridPositions)
            {
                gridPosition += p.X + "," + p.Y + " ";
            }

            sb.SetOldGridPos(gridPosition);

            return gridPositions;
        }
        private void SpawnFood()
        {
            Vector2 position = new Vector2(_rand.Next(_foodTexture.Width, Global.WORLD_SIZE - _foodTexture.Width), _rand.Next(_foodTexture.Height, Global.WORLD_SIZE - _foodTexture.Height));
            SpawnFood(position, _maxFoodLevel);
        }
        private void SpawnFood(Vector2 position, float maxHerbavoreLevel)
        {
            Food food = new Food();
            food.Texture = _foodTexture;
            food.Position = position;

            //When the creature dies it throws the food randomly around it, make sure that we do not have food out of the world spawning
            if (food.Bounds.Left < 0)
            {
                food.Position = new Vector2((float)Math.Ceiling(food.Texture.Width / 2.0), food.Position.Y);
            }
            else if (food.Bounds.Right > Global.WORLD_SIZE)
            {
                food.Position = new Vector2(Global.WORLD_SIZE - (float)Math.Ceiling(food.Texture.Width / 2.0), food.Position.Y);
            }

            if (food.Bounds.Top < 0)
            {
                food.Position = new Vector2(food.Position.X, (float)Math.Ceiling(food.Texture.Height / 2.0));
            }
            else if (food.Bounds.Bottom > Global.WORLD_SIZE)
            {
                food.Position = new Vector2(food.Position.X, Global.WORLD_SIZE - (float)Math.Ceiling(food.Texture.Height / 2.0));
            }

            food.GridPositions = GetGridPositionsForSpriteBase(food);

            //50% chance to apply a food level to the food
            if (_rand.Next(0, 100) > 50)
                food.FoodStrength = _rand.Next(1, (int)maxHerbavoreLevel);
            else
                food.FoodStrength = 1;

            food.DisplayText = "(" + food.FoodStrength + ")";
            food.TextSize = _foodFont.MeasureString(food.DisplayText);

            _gameData.Food.Add(food);
            _gameData.AddFoodToGrid(food);
        }
        private void SpawnStartingCreature()
        {
            Creature creature = new Creature();
            creature.InitNewCreature(_rand, ref _names, _speciesIdCounter, ref _creatureIdCtr);
            creature.Texture = _basicCreatureTexture;
            creature.Position = new Vector2(_rand.Next(creature.Texture.Width, Global.WORLD_SIZE - creature.Texture.Width), _rand.Next(creature.Texture.Height, Global.WORLD_SIZE - creature.Texture.Height));
            creature.GridPositions = GetGridPositionsForSpriteBase(creature);

            _gameData.Creatures.Add(creature);
            _gameData.AddCreatureToGrid(creature);

            _speciesIdCounter++;
        }
        private bool CheckForNearestFoodLocation(List<Point> gridPositions, Creature creature, ref Vector2 closest, ref float distance)
        {
            bool foundFood = false;

            for (int k = 0; k < gridPositions.Count; k++)
            {
                foreach (Food f in _gameData.MapGridData[gridPositions[k].X, gridPositions[k].Y].Food)
                {
                    if (f.FoodStrength <= creature.Herbavore)
                    {
                        float tmpDistance = Vector2.Distance(creature.Position, f.Position);
                        if (tmpDistance < distance)
                        {
                            foundFood = true;
                            closest = f.Position;
                            distance = tmpDistance;
                        }
                    }
                }
            }

            return foundFood;
        }
    }
}