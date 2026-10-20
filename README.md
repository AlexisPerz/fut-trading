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

Code signing policy: free code signing provided by SignPath.io, certificate by SignPath Foundation.
