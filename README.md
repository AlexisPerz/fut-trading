# FUT Trading

Application Windows d'aide au trading manuel sur le marché des transferts d'EA SPORTS FC Ultimate Team.

- Lit en lecture seule, dans un navigateur intégré (WebView2), Futbin (marché, fourrage, DCE, méta, évolutions, ventes réelles), les comptes X suivis (leaks) et la web app EA ouverte par l'utilisateur.
- Classe des opportunités d'achat et de revente rapides, calcule un plan de mass bidding et mesure le résultat de chaque conseil.
- N'automatise aucun achat ni aucune vente : sur la web app EA, l'application ne clique jamais et ne tape rien.
- Les données restent sur le PC, dans `data/base.json` à côté de l'exécutable.

## Compiler

```
dotnet publish FutTrading.csproj -c Release --self-contained false -o out
```

## Version signée

Le workflow `.github/workflows/build-sign.yml` compile l'application et la fait signer par SignPath
(configuration d'artefact dans `signpath/artifact-configuration.xml`). `get-signed.ps1` installe la
dernière version signée dans `..\FUT Trading` en gardant le dossier `data`.

## Code signing policy

Free code signing provided by [SignPath.io](https://about.signpath.io), certificate by [SignPath Foundation](https://signpath.org).

- Committers and reviewers: [AlexisPerz](https://github.com/AlexisPerz)
- Approvers: [AlexisPerz](https://github.com/AlexisPerz)

Every release is built by GitHub Actions from this repository and approved manually before signing.

## Privacy policy

This program will not transfer any information to other networked systems unless specifically requested by the user or the person installing or operating it. It only loads the websites the user opens in its built-in browser (Futbin, X, the EA web app) and stores its data locally in `data/base.json`.
