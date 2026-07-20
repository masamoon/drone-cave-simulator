# MVP audio sources

The playable MVP labels its recorded assembly library as:

`MVP AUDIO · CC0 FIELD RECORDINGS · BIGSOUNDBANK / JOSEPH SARDIN`

All source recordings below were downloaded from BigSoundBank on 2026-07-20. Each linked source page identifies the recording as CC0/public-domain-equivalent, permits commercial use and modification, and does not require attribution or an account. The project nevertheless preserves the source identity and hashes here.

License information: <https://bigsoundbank.com/licenses.html>

| Local source | BigSoundBank source | MVP use | SHA-256 |
|---|---|---|---|
| `BSB_0025_ElectronicSwitch.wav` | [#0025 Electronic switch](https://bigsoundbank.com/electronic-switch-s0025.html) | guidance capture/cancel | `2CEE5190B4F77EB2E73A1E689281028F3A71B50E1AF6F162E55BB27DCC263996` |
| `BSB_0054_PenCap.wav` | [#0054 Pen cap](https://bigsoundbank.com/pen-cap-s0054.html) | connector body movement, seating and removal | `E4833B475A2B246866A6CD16B17F0CE49BE49356C336F29B3FF4A3681B0D18AC` |
| `BSB_0321_SwitchFive.wav` | [#0321 Switch #5](https://bigsoundbank.com/switch-5-s0321.html) | connector detents, twist detents and final torque | `B155A53781380A0D0D5737B265B7B0C24BAF59575B824C9CFEA5F587725BE416` |
| `BSB_0629_VelcroTape.wav` | [#0629 Velcro tape](https://bigsoundbank.com/velcro-tape-s0629.html) | battery-strap tightening and release | `CB78C09F08A38F95364142EC76A1384990ADC3756A61D0C41C6F6366C0DD6043` |
| `BSB_0794_SmallRatchet.wav` | [#0794 Small ratchet](https://bigsoundbank.com/small-ratchet-s0794.html) | fastening and loosening movement | `73514F00FE02898AAA4F02B4FA165696BE77629CF09F5A70A1A421600E8B354F` |
| `BSB_1962_PlasticTie01.wav` | [#1962 Plastic handcuff #1](https://bigsoundbank.com/plastic-handcuff-1-s1962.html) | quiet strap-tension texture | `D0D52F66779C4B03ED428888E68480813EBAAF1067B0546DB4768C10AED11EA5` |
| `BSB_1963_PlasticTie02.wav` | [#1963 Plastic handcuff #2](https://bigsoundbank.com/plastic-handcuff-2-s1963.html) | quiet strap-tension texture | `D2F8B481C6AED859E90E69796E2CBEDDF3F266D8A9203ABFCA170AA7DCAECC2D` |
| `BSB_1964_PlasticTie03.wav` | [#1964 Plastic handcuff #3](https://bigsoundbank.com/plastic-handcuff-3-s1964.html) | quiet strap-tension texture | `790B0E015DE724BFA2848DC24E45B90C6D12C121EB959B81D86B22A50FBF9C63` |
| `BSB_1965_PlasticTie04.wav` | [#1965 Plastic handcuff #4](https://bigsoundbank.com/plastic-handcuff-4-s1965.html) | quiet strap-tension texture | `27A4232A7AEB4D39AB1D961EA7AFC4C0BFFD213B4C19F33F58B650E3DC8EDF09` |

## Processing

`AssemblyAudioProfile.asset` selects short regions from these recordings, folds stereo sources to mono, applies small playback-rate and gain variations, layers compatible body/detent sounds, fades region boundaries, and soft-limits the rendered one-shots. Source recordings are never presented as exact recordings of FPV-specific hardware; they are clearly labeled generic CC0 field recordings chosen to approximate the relevant material and mechanism.

Procedural synthesis remains only as a fallback for cues not covered by the recorded MVP profile or if that profile cannot be loaded.
