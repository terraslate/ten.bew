ten.bew
=======

Massively scalable async service bus and async web server for .net. written from the ground up.

Written using C# and .Net 4.5 with Visual Studio 2013 preview.

OK, so you'll need at least 2 machines to get the most out of this, 3 is my minimum recommendation since it is easier to   see the work shared out across more than one machine when a local machine chooses not to participate in work it spawned.

Start with the ten.bew project, specifically checking the App.Config to set things up with your Mac Address, IP and a Multicast Port that works for you and your network.  Note there is a connection string in there, it's kind of used for the POC. project at this stage against a schema that you can infer from the BusinessChunk.cs.

Note that the heart of this system is the massively parallel servicebus that distributes work between connected nodes that can be added into the mix and taken offline at anytime.

A webserver is built in and the port is hardcoded in there to listen on port 801 since i didn't yet add the configuration piece, it's a small part of the bigger proof. Hey this project started to prove a point, that Node.JS and Vert.x do not have the Edge. In fact it was initially known as Edge.x and then i figured it would collide with so many names out there what the hell. So web.net backwards sprang to mind.

Then note the POC.ten.bew.App.  This guy has a WWWRoot folder. This is an App that is added into the system through copy and paste along with that content. Note it contains a few .page files and these are written using some serialized JSON format. You might get the idea that namespaces and assemblies are mapped to a prefix and that prefix is then used to find a specific XXXChunk and this ultimately gets loaded and becomes the processing element of the page.  Chunks are similar to controls in ASP.Net.

It's all async and messages are passed between servers using the servicebus. There is even scope in a message to allow closest servers to be found using location data in the future and to filter out far servers. It's early days, but that index.page you will see can be processed by as many machines as you throw at this beast and as big as you make that page.  That was done to excercise the service bus and show that any part of a page can all be asynchronously distributed on the same machine and across a farm and a combined result then given back to the caller. 

There is also a caching server which runs along the service bus, and this can handle sharding. Again, it's early days,not completely written or thought about, but you will quickly see how the backbone of the system, the service bus, is pretty easy to plug any code into to produce a massively distributed solution to most any problem in just a few lines of code, including a distributed cache. Try posting a serialized CacheRequest (as JSON) to the "/caching" endpoint.

(note that messageProcessors can be reached through json according to their key in the ten.bew App.config file) hence /caching is the url because that is the name in the App.config.

Remeber each machien needs confguring for the MAC and the Multicast address, the connectionstring can point to a single database server or not (as you wish), and for Caching to work as you expect ensure that you set the shardStart and shardEnd so they do not overlap between machines.

It's early days, but the whole thing shows some big potential, and a desperate need for some refactoring, repolishing,  and redesign.



