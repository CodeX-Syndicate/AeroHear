# 🎶 AeroHear - Mode Système Audio

## 🆕 Nouvelle Fonctionnalité : Mode Système Audio

AeroHear peut maintenant capturer **tout l'audio de votre PC** et le diffuser simultanément vers vos périphériques Bluetooth ! 

### ✨ Comment ça marche

1. **Activez le Mode Système** : Cochez la case "Mode système (capture audio PC)"
2. **Sélectionnez vos périphériques** Bluetooth dans la liste
3. **Lancez n'importe quelle application audio** : YouTube, Spotify, jeux, vidéos...
4. **L'audio est diffusé en temps réel** vers tous vos périphériques sélectionnés !

### 🎯 Avantages du Mode Système

- ✅ **Compatible avec toutes les applications** : Plus besoin de charger des fichiers
- ✅ **Fonctionne avec les services de streaming** : Netflix, YouTube, Spotify, etc.
- ✅ **Audio de jeu en multi-périphériques** : Partagez l'audio de vos jeux
- ✅ **Conférences et appels** : Diffusez sur plusieurs haut-parleurs
- ✅ **Contrôle en temps réel** : Changez les périphériques sans redémarrer

### 🔧 Utilisation

#### Mode Système (Nouveau)
1. Sélectionnez vos périphériques Bluetooth
2. Ajustez les volumes et délais si nécessaire  
3. Cochez "Mode système (capture audio PC)"
4. Lancez votre application audio préférée
5. L'audio est automatiquement diffusé !

#### Mode Fichier (Existant)
1. Désactivez le mode système
2. Cliquez "Charger audio" pour sélectionner un fichier
3. Sélectionnez vos périphériques
4. Cliquez "Lire"

### ⚠️ Important

- **Évitez les boucles audio** : Désactivez ou baissez le volume du haut-parleur principal
- **Un mode à la fois** : Le mode système remplace la lecture de fichiers
- **Performance** : Le mode système utilise plus de ressources CPU

### 🖥️ Intégration Windows

AeroHear utilise l'API Windows Audio Session (WASAPI) pour capturer l'audio système. 

**Pour apparaître dans les Paramètres Son de Windows** comme un véritable périphérique audio, il faudrait développer un pilote audio virtuel Windows complet - ce qui dépasse le cadre de cette application portable.

Le mode système actuel offre une **fonctionnalité équivalente** en capturant tout l'audio PC !

### 🛠️ Technique

- **Capture Audio** : WASAPI Loopback
- **Routage Temps Réel** : BufferedWaveProvider + WaveOutEvent  
- **Format** : 44.1kHz, 16-bit, Stéréo
- **Latence** : < 50ms typique

---

*Cette fonctionnalité fait d'AeroHear une solution complète pour la diffusion audio multi-périphériques sur PC !*