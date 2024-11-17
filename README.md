# Unity Clones Syncer
Utilitary tool that allows to create clones projects and synchronize them.  
Usefull for multiplayer development.  
It supports cross-platform between master and clones projects.  
It also supports building from clones.  

The clones paths list is stored as a project setting, but you may not want it to be included in a git repository as it is a local setting with local usage.  
Then you should add the Following exclusion into your .gitignore file :  
`/[Pp]roject[Ss]ettings/Packages/com.kevcas.clones-syncer/`