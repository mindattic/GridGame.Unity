// Generated from python tools/polish_lint.py --hits; hits are per screen class.
window.UiStyleAudit = {
 "command": "python tools/polish_lint.py --hits",
 "rules": {
  "POL-K1": "Header: screen draws its own page title instead of Ui.Header",
  "POL-K2": "Body / footer: raw Ui.Window instead of a kit role (Panel, ListPanel, DetailPanel, Footer)",
  "POL-K3": "Icons: tappable (Ui.Button, Ui.MenuRow, new Button) with no KindIcon",
  "POL-K4": "Type and spacing: inline style.margin* / padding* set in a screen"
 },
 "notLinted": [
  "Empty state",
  "Confirm (only the Confirm helper class itself is linted)"
 ],
 "groups": [
  {
   "ticket": "GG-T1380",
   "name": "Vendor",
   "screens": [
    {
     "name": "Shop",
     "classes": [
      "ShopScreen"
     ],
     "hits": [
      {
       "rule": "POL-K3",
       "file": "Assets/GridGame/App/Screens/HubScreens.cs",
       "line": 878,
       "what": "Ui.Button without KindIcon"
      },
      {
       "rule": "POL-K3",
       "file": "Assets/GridGame/App/Screens/HubScreens.cs",
       "line": 881,
       "what": "Ui.Button without KindIcon"
      }
     ]
    },
    {
     "name": "Blacksmith",
     "classes": [
      "BlacksmithScreen"
     ],
     "hits": []
    },
    {
     "name": "Alchemist",
     "classes": [
      "AlchemistScreen"
     ],
     "hits": []
    },
    {
     "name": "Aetherist",
     "classes": [
      "AetheristScreen"
     ],
     "hits": [
      {
       "rule": "POL-K3",
       "file": "Assets/GridGame/App/Screens/HubScreens.cs",
       "line": 2231,
       "what": "Ui.Button without KindIcon"
      },
      {
       "rule": "POL-K3",
       "file": "Assets/GridGame/App/Screens/HubScreens.cs",
       "line": 2238,
       "what": "Ui.Button without KindIcon"
      },
      {
       "rule": "POL-K3",
       "file": "Assets/GridGame/App/Screens/HubScreens.cs",
       "line": 2240,
       "what": "Ui.Button without KindIcon"
      },
      {
       "rule": "POL-K3",
       "file": "Assets/GridGame/App/Screens/HubScreens.cs",
       "line": 2255,
       "what": "Ui.Button without KindIcon"
      },
      {
       "rule": "POL-K3",
       "file": "Assets/GridGame/App/Screens/HubScreens.cs",
       "line": 2262,
       "what": "Ui.Button without KindIcon"
      },
      {
       "rule": "POL-K3",
       "file": "Assets/GridGame/App/Screens/HubScreens.cs",
       "line": 2264,
       "what": "Ui.Button without KindIcon"
      }
     ]
    },
    {
     "name": "Enchantress",
     "classes": [
      "EnchantressScreen"
     ],
     "hits": []
    }
   ]
  },
  {
   "ticket": "GG-T1383",
   "name": "Items and collections",
   "screens": [
    {
     "name": "Items",
     "classes": [
      "ItemsScreen"
     ],
     "hits": [
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/HubScreens.cs",
       "line": 198,
       "what": "inline marginLeft"
      },
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/HubScreens.cs",
       "line": 219,
       "what": "inline marginRight"
      }
     ]
    },
    {
     "name": "Caravan",
     "classes": [
      "CaravanScreen"
     ],
     "hits": []
    },
    {
     "name": "Bounty",
     "classes": [
      "BountyScreen"
     ],
     "hits": []
    },
    {
     "name": "Bestiary",
     "classes": [
      "BestiaryScreen"
     ],
     "hits": [
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/HubScreens.cs",
       "line": 1877,
       "what": "inline paddingLeft"
      },
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/HubScreens.cs",
       "line": 1877,
       "what": "inline paddingRight"
      },
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/HubScreens.cs",
       "line": 1878,
       "what": "inline marginTop"
      },
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/HubScreens.cs",
       "line": 1889,
       "what": "inline marginLeft"
      },
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/HubScreens.cs",
       "line": 1889,
       "what": "inline marginRight"
      },
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/HubScreens.cs",
       "line": 1900,
       "what": "inline marginLeft"
      },
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/HubScreens.cs",
       "line": 1900,
       "what": "inline marginRight"
      },
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/HubScreens.cs",
       "line": 1900,
       "what": "inline marginTop"
      },
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/HubScreens.cs",
       "line": 1900,
       "what": "inline marginBottom"
      },
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/HubScreens.cs",
       "line": 1947,
       "what": "inline marginRight"
      }
     ]
    },
    {
     "name": "Settings",
     "classes": [
      "SettingsScreen"
     ],
     "hits": [
      {
       "rule": "POL-K3",
       "file": "Assets/GridGame/App/Screens/HubScreens.cs",
       "line": 1995,
       "what": "Ui.Button without KindIcon"
      },
      {
       "rule": "POL-K3",
       "file": "Assets/GridGame/App/Screens/HubScreens.cs",
       "line": 2001,
       "what": "Ui.Button without KindIcon"
      },
      {
       "rule": "POL-K3",
       "file": "Assets/GridGame/App/Screens/HubScreens.cs",
       "line": 2065,
       "what": "Ui.MenuRow without KindIcon"
      },
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/HubScreens.cs",
       "line": 1992,
       "what": "inline paddingLeft"
      }
     ]
    }
   ]
  },
  {
   "ticket": "GG-T1385",
   "name": "Journal and store",
   "screens": [
    {
     "name": "StageSelect",
     "classes": [
      "StageSelectScreen",
      "StageConfirmScreen"
     ],
     "hits": [
      {
       "rule": "POL-K3",
       "file": "Assets/GridGame/App/Screens/MenuScreens.cs",
       "line": 934,
       "what": "Ui.MenuRow without KindIcon"
      },
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/MenuScreens.cs",
       "line": 944,
       "what": "inline marginLeft"
      },
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/MenuScreens.cs",
       "line": 1021,
       "what": "inline marginLeft"
      },
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/MenuScreens.cs",
       "line": 1022,
       "what": "inline marginLeft"
      }
     ]
    },
    {
     "name": "Talk",
     "classes": [
      "TalkScreen"
     ],
     "hits": []
    },
    {
     "name": "Quest",
     "classes": [
      "QuestScreen"
     ],
     "hits": [
      {
       "rule": "POL-K2",
       "file": "Assets/GridGame/App/Screens/QuestScreen.cs",
       "line": 111,
       "what": "raw Ui.Window"
      }
     ]
    },
    {
     "name": "Codex",
     "classes": [
      "CodexScreen"
     ],
     "hits": [
      {
       "rule": "POL-K3",
       "file": "Assets/GridGame/App/Screens/CodexScreen.cs",
       "line": 85,
       "what": "Ui.MenuRow without KindIcon"
      },
      {
       "rule": "POL-K3",
       "file": "Assets/GridGame/App/Screens/CodexScreen.cs",
       "line": 244,
       "what": "Ui.MenuRow without KindIcon"
      },
      {
       "rule": "POL-K3",
       "file": "Assets/GridGame/App/Screens/CodexScreen.cs",
       "line": 250,
       "what": "Ui.MenuRow without KindIcon"
      },
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/CodexScreen.cs",
       "line": 122,
       "what": "inline marginRight"
      }
     ]
    },
    {
     "name": "Mercenary",
     "classes": [
      "MercenaryScreen",
      "MercenaryCamp",
      "MercenaryRenewalScreen"
     ],
     "hits": [
      {
       "rule": "POL-K2",
       "file": "Assets/GridGame/App/Screens/MercenaryScreens.cs",
       "line": 187,
       "what": "raw Ui.Window"
      },
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/MercenaryScreens.cs",
       "line": 65,
       "what": "inline marginTop"
      },
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/MercenaryScreens.cs",
       "line": 78,
       "what": "inline marginTop"
      },
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/MercenaryScreens.cs",
       "line": 78,
       "what": "inline marginBottom"
      },
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/MercenaryScreens.cs",
       "line": 78,
       "what": "inline marginLeft"
      },
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/MercenaryScreens.cs",
       "line": 78,
       "what": "inline marginRight"
      },
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/MercenaryScreens.cs",
       "line": 83,
       "what": "inline marginTop"
      },
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/MercenaryScreens.cs",
       "line": 83,
       "what": "inline marginBottom"
      },
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/MercenaryScreens.cs",
       "line": 83,
       "what": "inline marginLeft"
      },
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/MercenaryScreens.cs",
       "line": 83,
       "what": "inline marginRight"
      },
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/MercenaryScreens.cs",
       "line": 118,
       "what": "inline marginLeft"
      },
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/MercenaryScreens.cs",
       "line": 199,
       "what": "inline marginTop"
      }
     ]
    },
    {
     "name": "DiamondStore",
     "classes": [
      "DiamondStoreScreen",
      "SpendGate"
     ],
     "hits": [
      {
       "rule": "POL-K2",
       "file": "Assets/GridGame/App/Screens/DiamondStoreScreen.cs",
       "line": 81,
       "what": "raw Ui.Window"
      },
      {
       "rule": "POL-K3",
       "file": "Assets/GridGame/App/Screens/DiamondStoreScreen.cs",
       "line": 100,
       "what": "Ui.Button without KindIcon"
      },
      {
       "rule": "POL-K3",
       "file": "Assets/GridGame/App/Screens/DiamondStoreScreen.cs",
       "line": 127,
       "what": "Ui.Button without KindIcon"
      },
      {
       "rule": "POL-K3",
       "file": "Assets/GridGame/App/Screens/DiamondStoreScreen.cs",
       "line": 181,
       "what": "Ui.Button without KindIcon"
      },
      {
       "rule": "POL-K3",
       "file": "Assets/GridGame/App/Screens/DiamondStoreScreen.cs",
       "line": 189,
       "what": "Ui.Button without KindIcon"
      },
      {
       "rule": "POL-K3",
       "file": "Assets/GridGame/App/Screens/DiamondStoreScreen.cs",
       "line": 191,
       "what": "Ui.Button without KindIcon"
      },
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/DiamondStoreScreen.cs",
       "line": 96,
       "what": "inline marginTop"
      },
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/DiamondStoreScreen.cs",
       "line": 111,
       "what": "inline marginTop"
      },
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/DiamondStoreScreen.cs",
       "line": 123,
       "what": "inline marginTop"
      },
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/DiamondStoreScreen.cs",
       "line": 137,
       "what": "inline marginTop"
      },
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/DiamondStoreScreen.cs",
       "line": 169,
       "what": "inline marginRight"
      }
     ]
    }
   ]
  },
  {
   "ticket": "GG-T1390",
   "name": "Hero and results",
   "screens": [
    {
     "name": "HeroScreen tabs",
     "classes": [
      "HeroScreen",
      "StatusTab",
      "AbilitiesTab",
      "EquipTab",
      "PartyTab"
     ],
     "hits": [
      {
       "rule": "POL-K2",
       "file": "Assets/GridGame/App/Screens/HeroScreen.cs",
       "line": 99,
       "what": "raw Ui.Window"
      },
      {
       "rule": "POL-K2",
       "file": "Assets/GridGame/App/Screens/HubScreens.cs",
       "line": 711,
       "what": "raw Ui.Window"
      },
      {
       "rule": "POL-K3",
       "file": "Assets/GridGame/App/Screens/HubScreens.cs",
       "line": 336,
       "what": "Ui.MenuRow without KindIcon"
      },
      {
       "rule": "POL-K3",
       "file": "Assets/GridGame/App/Screens/HubScreens.cs",
       "line": 555,
       "what": "Ui.MenuRow without KindIcon"
      },
      {
       "rule": "POL-K3",
       "file": "Assets/GridGame/App/Screens/HubScreens.cs",
       "line": 642,
       "what": "Ui.MenuRow without KindIcon"
      },
      {
       "rule": "POL-K3",
       "file": "Assets/GridGame/App/Screens/HubScreens.cs",
       "line": 667,
       "what": "Ui.MenuRow without KindIcon"
      },
      {
       "rule": "POL-K3",
       "file": "Assets/GridGame/App/Screens/HubScreens.cs",
       "line": 680,
       "what": "Ui.MenuRow without KindIcon"
      },
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/HeroScreen.cs",
       "line": 101,
       "what": "inline marginTop"
      },
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/HeroScreen.cs",
       "line": 104,
       "what": "inline paddingLeft"
      },
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/HeroScreen.cs",
       "line": 104,
       "what": "inline paddingRight"
      },
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/HeroScreen.cs",
       "line": 105,
       "what": "inline paddingTop"
      },
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/HeroScreen.cs",
       "line": 105,
       "what": "inline paddingBottom"
      },
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/HeroScreen.cs",
       "line": 114,
       "what": "inline paddingLeft"
      },
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/HeroScreen.cs",
       "line": 114,
       "what": "inline paddingRight"
      },
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/HeroScreen.cs",
       "line": 127,
       "what": "inline marginTop"
      },
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/HeroScreen.cs",
       "line": 127,
       "what": "inline marginBottom"
      },
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/HeroScreen.cs",
       "line": 127,
       "what": "inline marginLeft"
      },
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/HeroScreen.cs",
       "line": 127,
       "what": "inline marginRight"
      },
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/HeroScreen.cs",
       "line": 128,
       "what": "inline paddingTop"
      },
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/HeroScreen.cs",
       "line": 128,
       "what": "inline paddingBottom"
      },
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/HeroScreen.cs",
       "line": 128,
       "what": "inline paddingLeft"
      },
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/HeroScreen.cs",
       "line": 128,
       "what": "inline paddingRight"
      },
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/HeroScreen.cs",
       "line": 132,
       "what": "inline marginRight"
      },
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/HeroScreen.cs",
       "line": 142,
       "what": "inline marginLeft"
      },
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/HubScreens.cs",
       "line": 257,
       "what": "inline marginLeft"
      },
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/HubScreens.cs",
       "line": 501,
       "what": "inline paddingLeft"
      },
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/HubScreens.cs",
       "line": 501,
       "what": "inline paddingRight"
      },
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/HubScreens.cs",
       "line": 508,
       "what": "inline marginLeft"
      },
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/HubScreens.cs",
       "line": 508,
       "what": "inline marginRight"
      },
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/HubScreens.cs",
       "line": 518,
       "what": "inline marginRight"
      },
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/HubScreens.cs",
       "line": 519,
       "what": "inline marginRight"
      }
     ]
    },
    {
     "name": "SkillHexTab",
     "classes": [
      "SkillHexTab"
     ],
     "hits": []
    },
    {
     "name": "PartyBoards",
     "classes": [
      "PartyBoards"
     ],
     "hits": [
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/PartyBoards.cs",
       "line": 44,
       "what": "inline marginLeft"
      },
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/PartyBoards.cs",
       "line": 44,
       "what": "inline marginRight"
      },
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/PartyBoards.cs",
       "line": 64,
       "what": "inline marginTop"
      },
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/PartyBoards.cs",
       "line": 73,
       "what": "inline marginLeft"
      },
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/PartyBoards.cs",
       "line": 73,
       "what": "inline marginRight"
      },
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/PartyBoards.cs",
       "line": 74,
       "what": "inline marginBottom"
      }
     ]
    },
    {
     "name": "LevelUp",
     "classes": [
      "LevelUpScreen"
     ],
     "hits": [
      {
       "rule": "POL-K1",
       "file": "Assets/GridGame/App/Screens/LevelUpScreen.cs",
       "line": 60,
       "what": "own page title"
      },
      {
       "rule": "POL-K1",
       "file": "Assets/GridGame/App/Screens/LevelUpScreen.cs",
       "line": 78,
       "what": "own page title"
      },
      {
       "rule": "POL-K1",
       "file": "Assets/GridGame/App/Screens/LevelUpScreen.cs",
       "line": 81,
       "what": "own page title"
      },
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/LevelUpScreen.cs",
       "line": 63,
       "what": "inline marginBottom"
      },
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/LevelUpScreen.cs",
       "line": 77,
       "what": "inline marginLeft"
      },
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/LevelUpScreen.cs",
       "line": 80,
       "what": "inline marginBottom"
      },
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/LevelUpScreen.cs",
       "line": 83,
       "what": "inline marginTop"
      },
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/LevelUpScreen.cs",
       "line": 86,
       "what": "inline marginTop"
      }
     ]
    },
    {
     "name": "PostBattle",
     "classes": [
      "PostBattleScreen"
     ],
     "hits": [
      {
       "rule": "POL-K1",
       "file": "Assets/GridGame/App/Screens/PostBattleScreen.cs",
       "line": 67,
       "what": "own page title"
      },
      {
       "rule": "POL-K1",
       "file": "Assets/GridGame/App/Screens/PostBattleScreen.cs",
       "line": 210,
       "what": "own page title"
      },
      {
       "rule": "POL-K1",
       "file": "Assets/GridGame/App/Screens/PostBattleScreen.cs",
       "line": 213,
       "what": "own page title"
      },
      {
       "rule": "POL-K2",
       "file": "Assets/GridGame/App/Screens/PostBattleScreen.cs",
       "line": 72,
       "what": "raw Ui.Window"
      },
      {
       "rule": "POL-K2",
       "file": "Assets/GridGame/App/Screens/PostBattleScreen.cs",
       "line": 226,
       "what": "raw Ui.Window"
      },
      {
       "rule": "POL-K3",
       "file": "Assets/GridGame/App/Screens/PostBattleScreen.cs",
       "line": 78,
       "what": "Ui.MenuRow without KindIcon"
      },
      {
       "rule": "POL-K3",
       "file": "Assets/GridGame/App/Screens/PostBattleScreen.cs",
       "line": 82,
       "what": "Ui.MenuRow without KindIcon"
      },
      {
       "rule": "POL-K3",
       "file": "Assets/GridGame/App/Screens/PostBattleScreen.cs",
       "line": 96,
       "what": "Ui.MenuRow without KindIcon"
      },
      {
       "rule": "POL-K3",
       "file": "Assets/GridGame/App/Screens/PostBattleScreen.cs",
       "line": 102,
       "what": "Ui.MenuRow without KindIcon"
      },
      {
       "rule": "POL-K3",
       "file": "Assets/GridGame/App/Screens/PostBattleScreen.cs",
       "line": 106,
       "what": "Ui.MenuRow without KindIcon"
      },
      {
       "rule": "POL-K3",
       "file": "Assets/GridGame/App/Screens/PostBattleScreen.cs",
       "line": 109,
       "what": "Ui.MenuRow without KindIcon"
      },
      {
       "rule": "POL-K3",
       "file": "Assets/GridGame/App/Screens/PostBattleScreen.cs",
       "line": 120,
       "what": "Ui.MenuRow without KindIcon"
      },
      {
       "rule": "POL-K3",
       "file": "Assets/GridGame/App/Screens/PostBattleScreen.cs",
       "line": 121,
       "what": "Ui.MenuRow without KindIcon"
      },
      {
       "rule": "POL-K3",
       "file": "Assets/GridGame/App/Screens/PostBattleScreen.cs",
       "line": 122,
       "what": "Ui.MenuRow without KindIcon"
      },
      {
       "rule": "POL-K3",
       "file": "Assets/GridGame/App/Screens/PostBattleScreen.cs",
       "line": 124,
       "what": "Ui.MenuRow without KindIcon"
      },
      {
       "rule": "POL-K3",
       "file": "Assets/GridGame/App/Screens/PostBattleScreen.cs",
       "line": 151,
       "what": "Ui.MenuRow without KindIcon"
      },
      {
       "rule": "POL-K3",
       "file": "Assets/GridGame/App/Screens/PostBattleScreen.cs",
       "line": 155,
       "what": "Ui.MenuRow without KindIcon"
      },
      {
       "rule": "POL-K3",
       "file": "Assets/GridGame/App/Screens/PostBattleScreen.cs",
       "line": 162,
       "what": "Ui.MenuRow without KindIcon"
      },
      {
       "rule": "POL-K3",
       "file": "Assets/GridGame/App/Screens/PostBattleScreen.cs",
       "line": 165,
       "what": "Ui.MenuRow without KindIcon"
      },
      {
       "rule": "POL-K3",
       "file": "Assets/GridGame/App/Screens/PostBattleScreen.cs",
       "line": 167,
       "what": "Ui.MenuRow without KindIcon"
      },
      {
       "rule": "POL-K3",
       "file": "Assets/GridGame/App/Screens/PostBattleScreen.cs",
       "line": 234,
       "what": "Ui.MenuRow without KindIcon"
      },
      {
       "rule": "POL-K3",
       "file": "Assets/GridGame/App/Screens/PostBattleScreen.cs",
       "line": 236,
       "what": "Ui.MenuRow without KindIcon"
      },
      {
       "rule": "POL-K3",
       "file": "Assets/GridGame/App/Screens/PostBattleScreen.cs",
       "line": 237,
       "what": "Ui.MenuRow without KindIcon"
      },
      {
       "rule": "POL-K3",
       "file": "Assets/GridGame/App/Screens/PostBattleScreen.cs",
       "line": 279,
       "what": "Ui.MenuRow without KindIcon"
      },
      {
       "rule": "POL-K3",
       "file": "Assets/GridGame/App/Screens/PostBattleScreen.cs",
       "line": 293,
       "what": "Ui.MenuRow without KindIcon"
      },
      {
       "rule": "POL-K3",
       "file": "Assets/GridGame/App/Screens/PostBattleScreen.cs",
       "line": 337,
       "what": "Ui.MenuRow without KindIcon"
      },
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/PostBattleScreen.cs",
       "line": 63,
       "what": "inline paddingTop"
      },
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/PostBattleScreen.cs",
       "line": 63,
       "what": "inline paddingBottom"
      },
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/PostBattleScreen.cs",
       "line": 73,
       "what": "inline marginTop"
      },
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/PostBattleScreen.cs",
       "line": 227,
       "what": "inline marginTop"
      },
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/PostBattleScreen.cs",
       "line": 330,
       "what": "inline marginLeft"
      },
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/PostBattleScreen.cs",
       "line": 330,
       "what": "inline marginBottom"
      },
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/PostBattleScreen.cs",
       "line": 353,
       "what": "inline marginTop"
      },
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/PostBattleScreen.cs",
       "line": 353,
       "what": "inline marginBottom"
      },
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/PostBattleScreen.cs",
       "line": 354,
       "what": "inline marginRight"
      },
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/PostBattleScreen.cs",
       "line": 362,
       "what": "inline marginTop"
      },
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/PostBattleScreen.cs",
       "line": 408,
       "what": "inline marginTop"
      },
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/PostBattleScreen.cs",
       "line": 408,
       "what": "inline marginBottom"
      },
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/PostBattleScreen.cs",
       "line": 409,
       "what": "inline marginRight"
      },
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/PostBattleScreen.cs",
       "line": 424,
       "what": "inline marginRight"
      },
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/PostBattleScreen.cs",
       "line": 437,
       "what": "inline marginTop"
      },
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/PostBattleScreen.cs",
       "line": 449,
       "what": "inline marginTop"
      },
      {
       "rule": "POL-K4",
       "file": "Assets/GridGame/App/Screens/PostBattleScreen.cs",
       "line": 449,
       "what": "inline marginRight"
      }
     ]
    }
   ]
  }
 ],
 "confirm": [
  {
   "rule": "POL-K2",
   "file": "Assets/GridGame/App/Screens/MenuScreens.cs",
   "line": 493,
   "what": "raw Ui.Window"
  },
  {
   "rule": "POL-K4",
   "file": "Assets/GridGame/App/Screens/MenuScreens.cs",
   "line": 499,
   "what": "inline marginTop"
  }
 ],
 "outOfScope": [
  "Title",
  "SaveSlot",
  "CharacterSelect",
  "Dialogue",
  "StoryCrawl",
  "battle HUD"
 ],
 "unmapped": {
  "CharacterSelectScreen": 18,
  "Confirm": 2,
  "CreditsScreen": 3,
  "CrucibleScreen": 5,
  "DialogueScreen": 13,
  "HubScreen": 32,
  "ProfileNameScreen": 4,
  "SaveTransferUi": 4,
  "StoryCrawlScreen": 2,
  "SummonReveal": 3,
  "SummonScreen": 3,
  "TitleScreen": 4,
  "WindowEditorScreen": 46
 }
};
