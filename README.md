🌽 Dynamic Valley Economy

Dynamic Valley Economy is a lore-friendly economic overhaul for Stardew Valley. It introduces financial realism by adding progressive taxation, property taxes, and a volatile market system that reacts to your farming choices.
🛠 Features
📈 Progressive Wealth Taxation

Say goodbye to being an untaxed millionaire. The mod calculates taxes based on your current gold holdings:

    Starter (5k+): A tiny maintenance fee for new farmers.

    Humble (25k+): Contributing to local infrastructure.

    Comfort (100k+): Helping the Valley thrive.

    Wealthy (1M+): Higher tier for established entrepreneurs.

    Tycoon (10M+): Significant taxation for the Valley's elite.

Tax Frequency: Fully configurable to Daily, Weekly (every Sunday), or Bi-Weekly (14th and 28th).
🏠 Property Taxes

On the 28th of every month, Mayor Lewis visits your farm to collect property taxes based on the number of buildings (Barns, Coops, Sheds, etc.) you own.
📉 Market Flooding & Saturation

Selling 500 ancient fruit wine in a single night will now crash the market!

    Saturation Limit: Set a limit on how much of one item can be sold before the price drops.

    Persistent Crashes: Markets don't recover overnight. Crashes last for a configurable amount of days (default 7).

    Luxury & Subsidies: Toggle specific crops as "Luxury" (higher tax) or "Subsidized" (price bonus) via the config menu.

📺 Economic Reports & Ledger

Stay informed using your Farmhouse TV:

    Economic Forecast: See which markets are currently crashed and how many days until they recover.

    Tax Ledger: View a history of your last 10 tax payments to track your contributions to the Valley.

⚙️ Configuration

This mod fully supports Generic Mod Config Menu (GMCM). You can adjust all tax rates, crash durations, and crop statuses directly in the game's options menu.
🚀 Installation

    Install the latest version of SMAPI.

    Install Generic Mod Config Menu.

    Download Dynamic Valley Economy and drop the folder into your Mods directory.

    Run the game using SMAPI.

🏗 Modular Structure (For Developers)

The source code is organized into modular functions to ensure UI stability:

    BuildMainMenu: Handles wealth brackets and scheduling.

    BuildSeasonalPages: Generates independent sub-menus for Spring, Summer, Fall, and Winter crops.

    OnDayStarted: Processes scheduling math and tax deductions.
