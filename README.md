# wCalculator

Une calculatrice qui reprend la **Calculatrice Windows de Microsoft** et la fait tourner partout : **Windows, Linux, macOS et Android**,
avec un seul code d'interface écrit en C# avec [Avalonia UI](https://avaloniaui.net/) sur .NET 10.

## But du projet

La Calculatrice Windows est l'une des meilleures calculatrices grand public, mais elle n'existe que sur Windows.
Microsoft en a publié le code sous licence MIT ([`microsoft/calculator`](https://github.com/microsoft/calculator)),
mais il est écrit en C++/WinRT et en XAML UWP : il ne tourne nulle part ailleurs.

wCalculator en est un portage : même moteur de calcul, mêmes modes, mêmes résultats, même interface générale, sur toutes les plateformes.
Le moteur d'origine a été **réécrit en C#** pour rester fidèle à son comportement
(précision arbitraire, ordre des opérations, messages d'erreur), sans aucune bibliothèque native.

## État du projet

Les fonctions suivantes sont portées :

* **Standard** : calcul à la chaîne, comme l'original (sans priorité des opérateurs).
* **Scientifique** : priorités, parenthèses, trigonométrie et hyperbolique, touche 2nd, degrés / radians / grades, notation F-E.
* **Programmeur** : HEX / DEC / OCT / BIN, tailles de mot QWORD à BYTE, opérations bit à bit, rotations, panneau de bits.
* **Calcul de dates** : différence entre deux dates, ajout et soustraction.
* **Convertisseurs** : volume, longueur, poids et masse, température, énergie, aire, vitesse, temps, puissance, données, pression, angle et **devises**.
* **Historique** (Standard et Scientifique) et **mémoire** à 100 emplacements.
* Clavier physique, copier / coller.
* **60 langues** pour l'interface de la calculatrice (chaînes extraites des ressources d'origine, langue du système).

Ajouts par rapport à l'original :

* **Devises** : environ 150 monnaies, taux du jour, utilisables hors ligne avec les derniers taux connus.
* **Page Paramètres** : thème (appareil, sombre, clair), couleur d'accent, transparence de la fenêtre sur le bureau,
  transparence et flou du menu, retour haptique sur mobile.
* **Mise en page paysage** pour les téléphones tournés et les fenêtres larges.
* Icônes [Phosphor](https://phosphoricons.com).

Ce qui n'est **pas** porté :

* **Le mode Graphique.** Il repose sur un moteur de tracé qui n'est pas publié dans le dépôt d'origine.

Le moteur est couvert par des tests xUnit (calcul, programmeur, dates, convertisseurs, devises). L'interface n'a pas de tests automatiques.

## Devises

Les taux viennent de la **currency-api de fawazahmed0** ([dépôt](https://github.com/fawazahmed0/exchange-api), CC0) :
gratuite, sans clé, sans limite de requêtes, mise à jour chaque jour. Deux miroirs sont essayés dans l'ordre
(`cdn.jsdelivr.net`, puis `latest.currency-api.pages.dev`).

Les taux (base : euro) sont téléchargés à l'ouverture de la catégorie « Devises », au plus toutes les 6 heures,
et gardés dans `currency-rates.json` du dossier de données de l'utilisateur (`%LocalAppData%\wCalculator`, `~/.local/share/wCalculator`, …).
Sans réseau, les derniers taux connus restent utilisés. Seules les monnaies ISO 4217 sont proposées. Les taux sont indicatifs.

## Organisation du dépôt

```
src/Calculator.Engine   moteur de calcul, convertisseurs, dates (net10.0, aucune dépendance)
src/Calculator.App      interface Avalonia partagée : vues, view-models, styles, localisation, réglages, devises
src/Calculator.Desktop  exécutable Windows / Linux / macOS
src/Calculator.Android  application Android
tests/                  tests xUnit du moteur
tools/                  générateurs de code (constantes ratpak, commandes, chaînes, XAML du clavier) et script du .app macOS
packaging/              fichiers des installeurs (script NSIS, .desktop et métadonnées AppStream)
build.sh                construit les installeurs dans ./distribution
```

Le moteur reprend l'architecture d'origine : nombres rationnels à précision arbitraire (*ratpak*), machine à états `CCalcEngine`,
collecteur d'historique, gestionnaire de mémoire, `UnitConverter` et `DateCalculationEngine`.

## Compiler

Prérequis : le SDK **.NET 10**. Pour Android, il faut en plus `dotnet workload install android` et un JDK.

```bash
dotnet test tests/Calculator.Engine.Tests      # tests du moteur
dotnet run  --project src/Calculator.Desktop   # lancer sur le poste
```

### Installeurs

```bash
./build.sh                    # toutes les cibles
./build.sh windows android    # seulement celles-ci
```

Les fichiers sont écrits dans `./distribution`, sous la forme `wCalculator_<version>.<extension>`. La version est le `<Version>`
de `Directory.Build.props`. Si l'outil d'une cible manque, le script le dit et passe aux suivantes. Tout se construit depuis Linux.

| Cible | Fichier | Outils nécessaires |
|---|---|---|
| Linux (Flatpak) | `wCalculator_<version>.flatpak` | `dotnet`, `flatpak`, `flatpak-builder` |
| Linux (AppImage) | `wCalculator_<version>.AppImage` | `dotnet`, `curl` (`appimagetool` est téléchargé dans `build/` s'il manque) |
| Debian / Ubuntu | `wCalculator_<version>.deb` (amd64) | `dotnet`, `ar` (binutils), `tar`, `xz` |
| Arch Linux | `wCalculator_<version>.pkg.tar.zst` (x86_64) | `dotnet`, `tar`, `zstd` |
| Windows | `wCalculator_<version>.exe` (installeur NSIS, x64) | `dotnet`, `makensis` (paquet `nsis`) |
| Android | `wCalculator_<version>.apk` | `dotnet` + workload `android`, JDK |
| macOS | `wCalculator_<version>.zip` (un `.app` pour Apple Silicon) | `dotnet`, `zip` |

Remarques :

* Les applications sont publiées avec leur runtime .NET, mais pas en « fichier unique » : l'installeur copie le dossier.
* **.deb et .pkg.tar.zst** : assemblés directement par le script (pas besoin de `dpkg-deb` ni de `makepkg`). Ils installent l'application dans `/usr/lib/wcalculator`, un lanceur `wCalculator` dans `/usr/bin`, l'entrée de menu et l'icône. Le nom du mainteneur se règle avec `MAINTAINER="Nom <mail>" ./build.sh deb`.
* **Flatpak** : le runtime freedesktop (25.08 par défaut, modifiable avec `FLATPAK_RUNTIME_VERSION`) est installé depuis Flathub lors de la première installation.
* **Android** : le SDK signe l'APK avec sa clé de débogage, ce qui suffit pour l'installer. Pour un store, utilisez votre propre keystore.
* **macOS** : l'application n'est ni signée ni notarisée. Au premier lancement : clic droit puis Ouvrir, ou
  `xattr -dr com.apple.quarantine wCalculator.app`. `MAC_ARCH=x64 ./build.sh macos` produit la version Intel (`_x64.zip`).

### Fichiers générés

Certains fichiers sont produits par des scripts Python depuis le dépôt d'origine. Il n'y a rien à faire pour compiler ; ces scripts ne servent
qu'à les régénérer :

```bash
python3 tools/gen_strings.py <microsoft/calculator>/src/Calculator/Resources src/Calculator.App/Localization/Strings
python3 tools/gen_calculator_view.py src/Calculator.App/Views/CalculatorView.axaml
```

## Données de l'utilisateur

Les réglages (`settings.json`) et le cache des taux de change sont enregistrés dans le dossier de données de l'utilisateur
(`%LocalAppData%\wCalculator` sous Windows, `~/.local/share/wCalculator` sous Linux). Rien n'est envoyé à un serveur, hormis le téléchargement des taux de change.

## Licences

| Élément | Licence |
|---|---|
| Calculatrice Windows d'origine (© Microsoft Corporation) : conception, moteur C++ dont ce code est dérivé | MIT, texte dans [`LICENSE-Microsoft-Calculator.txt`](LICENSE-Microsoft-Calculator.txt) |
| Code ajouté par ce portage | MIT, comme le code d'origine |
| [Avalonia UI](https://avaloniaui.net/) | MIT |
| [Phosphor Icons](https://phosphoricons.com) (polices dans `src/Calculator.App/Assets/Fonts/Phosphor`) | MIT, voir `LICENSE-Phosphor.txt` |
| [currency-api](https://github.com/fawazahmed0/exchange-api) (données des taux de change) | CC0 |

**Marques et logo.** La licence MIT ne concède aucun droit sur les marques et logos de Microsoft. L'icône actuelle de l'application
est le logo de la Calculatrice Windows : elle est utilisée ici pour un usage personnel. Avant toute diffusion publique,
remplacez-la par une icône à vous : `src/Calculator.App/Assets/icon.png`, `src/Calculator.App/Assets/icon.ico` et
`src/Calculator.Android/Resources/drawable/icon.png`. Ce projet n'est ni affilié à Microsoft ni approuvé par elle.
