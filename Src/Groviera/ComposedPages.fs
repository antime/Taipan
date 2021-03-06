﻿namespace ES.Groviera

open System
open Suave
open Suave.Filters
open Suave.Successful
open Suave.Writers
open Suave.Operators
open Suave.RequestErrors
open Suave.Authentication
open ES.Groviera.Utility

module ComposedPages =
    let mutable private _journeyInSession = true

    let index (ctx: HttpContext) =
        OK """<html>
  <head><title>Groviera Web App - Web Application Composed tests</title></head>
  <body>
    This section is used in order to test for the integration of the various components.</br>
	<h3>Follow a series of test cases:</h3>
	<ul>
		<li>TEST1: <a href="/composed/test1/">/composed/test1/</a></li>
        <li>TEST2: <a href="/composed/test2/">/composed/test2/</a></li>
        <li>TEST3: <a href="/composed/test3/">/composed/test3/</a></li>
        <li>TEST4: <a href="/composed/test4/">/composed/test4/</a></li>
        <li>TEST5: <a href="/composed/test5/">/composed/test5/</a></li>
        <li>TEST6: <a href="/composed/test6/">/composed/test6/</a></li>
        <li>TEST7: <a href="/composed/test7/">/composed/test7/</a></li>
        <li>TEST8: <a href="/composed/test8/">/composed/test8/</a></li>
        <li>TEST9: <a href="/composed/test9/">/composed/test9/</a></li>
        <li>TEST10: <a href="/composed/test10/">/composed/test10/</a></li>
        <li>TEST11: <a href="/composed/test11/">/composed/test11/</a></li>
	</ul><br/>
  </body>
</html>""" ctx

    let getComposedRoutes() = 
        choose [       
            GET >=> choose [
                path "/composed/" >=> index
                path "/composed/test1/" >=> test "Discover an hidden directory and identify a know web application"
                path "/composed/test2/" >=> test "Discover an hidden directory and identify a know web application with a plugin"
                path "/composed/test3/" >=> test "Crawl to a given link and discover a new directory, Start <a href='/composed/test3/link.html'>here</a>"
                path "/composed/test4/" >=> test "Discover an hidden directory and navigate to a link"
                path "/composed/test5/" >=> test "Crawl a link and fingerprint a web application, Start <a href='/composed/test5/link.html'>here</a>"
                path "/composed/test6/" >=> test "Crawl a link, discover an hidden resources and fingerprint a web application, Start <a href='/composed/test6/link.html'>here</a>"
                path "/composed/test7/" >=> test "Discover an hidden resource, crawl a link and fingerprint a web application"
                path "/composed/test8/" >=> test "Crawl a link, discover an hidden resources and found a vulnerability, Start <a href='/composed/test8/link.html'>here</a>"
                path "/composed/test9/" >=> test "Discover an hidden resource, crawl a link and found a vulnerability via link mutation"                
                path "/composed/test10/" >=> test "Crawl a link, discover an hidden resources, found a vulnerability via link mutation and fingerprint a web application. Start <a href='/composed/test10/link.html'>here</a>"

                path "/composed/test1/admin/" >=> ok
                path "/composed/test1/admin/htaccess.txt" >=> ok
                path "/composed/test1/admin/joomla344version.html" >=> okContent "Joomla version 3.4.4"

                path "/composed/test2/admin/" >=> ok
                path "/composed/test2/admin/htaccess.txt" >=> ok
                path "/composed/test2/admin/joomla344version.html" >=> okContent "Joomla version 3.4.4"
                path "/composed/test2/admin/plugin/dir/foo.html" >=> ok
                path "/composed/test2/admin/plugin/dir/plugin.php" >=> okContent "Joomla awesome plugin version 0.1.0"

                path "/composed/test3/link.html" >=> okContent "Here is another <a href='/composed/test3/hdndir/page.html'>link</a>"
                path "/composed/test3/hdndir/page.html" >=> ok
                path "/composed/test3/hdndir/admin/" >=> ok

                path "/composed/test4/admin/" >=> okContent "Here is another <a href='/composed/test4/admin/page.html'>link</a>"
                path "/composed/test4/admin/page.html" >=> ok

                path "/composed/test5/link.html" >=> okContent "Here is another <a href='/composed/test5/admin/page.html'>link</a>"
                path "/composed/test5/admin/page.html" >=> ok
                path "/composed/test5/admin/htaccess.txt" >=> ok
                path "/composed/test5/admin/joomla344version.html" >=> okContent "Joomla version 3.4.4"
                path "/composed/test5/admin/plugin/dir/foo.html" >=> ok
                path "/composed/test5/admin/plugin/dir/plugin.php" >=> okContent "Joomla awesome plugin version 0.1.0"

                path "/composed/test6/link.html" >=> okContent "Here is another <a href='/composed/test6/foo/page.html'>link</a>"
                path "/composed/test6/foo/page.html" >=> ok
                path "/composed/test6/foo/admin/" >=> ok
                path "/composed/test6/foo/admin/htaccess.txt" >=> ok
                path "/composed/test6/foo/admin/joomla344version.html" >=> okContent "Joomla version 3.4.4"
                path "/composed/test6/foo/admin/plugin/dir/foo.html" >=> ok
                path "/composed/test6/foo/admin/plugin/dir/plugin.php" >=> okContent "Joomla awesome plugin version 0.1.0"

                path "/composed/test7/admin/" >=> okContent "Here is another link <a href='/composed/test7/admin/foo/page.html'>link</a>"
                path "/composed/test7/admin/foo/page.html" >=> ok                
                path "/composed/test7/admin/foo/htaccess.txt" >=> ok
                path "/composed/test7/admin/foo/joomla344version.html" >=> okContent "Joomla version 3.4.4"
                path "/composed/test7/admin/foo/plugin/dir/foo.html" >=> ok
                path "/composed/test7/admin/foo/plugin/dir/plugin.php" >=> okContent "Joomla awesome plugin version 0.1.0"

                path "/composed/test8/link.html" >=> okContent "Here is another <a href='/composed/test8/foo/page.html'>link</a>"
                path "/composed/test8/foo/page.html" >=> ok
                path "/composed/test8/foo/admin/" >=> okContent "Identify a directory listing!!! Content: <h1>Index of /kubuntu/releases</h1>"   
                
                path "/composed/test9/admin/" >=> okContent "Here is another link <a href='/composed/test9/admin/foo/page.html'>link</a>"
                path "/composed/test9/admin/foo/page.html" >=> ok
                path "/composed/test9/admin/foo/" >=> okContent "Identify a directory listing: <h1>Index of /kubuntu/releases</h1>" 

                path "/composed/test10/link.html" >=> okContent "Here is another: <a href='/composed/test10/foo/page.html'>link</a>"
                path "/composed/test10/foo/page.html" >=> ok
                path "/composed/test10/foo/admin/" >=> okContent "Still another link: <a href='/composed/test10/foo/admin/dirlisting/page.html'>link</a>"
                path "/composed/test10/foo/admin/dirlisting/page.html" >=> ok
                path "/composed/test10/foo/admin/dirlisting/" >=> okContent "Identify a directory listing: <h1>Index of /kubuntu/releases</h1>"   
                path "/composed/test10/foo/admin/htaccess.txt" >=> ok
                path "/composed/test10/foo/admin/joomla344version.html" >=> okContent "Joomla version 3.4.4"
                path "/composed/test10/foo/admin/plugin/dir/foo.html" >=> ok
                path "/composed/test10/foo/admin/plugin/dir/plugin.php" >=> okContent "Joomla awesome plugin version 0.1.0"

                path "/composed/test11/" >=> okContent "This page will test for Journey Scan, to start <a href='/composed/test11/start'>visit this page</a>"
                path "/composed/test11/start" >=> okContent """To procede to next page you have to provide to be an 31337 hacker.
                <form action='/composed/test11/validate' method='POST'>
                    Insert the 31337 code here: <input type='text' name='code'>
                    <input type='submit' value='Send'>
                </form>
                """                
                path "/composed/test11/final" >=> fun (ctx: HttpContext) -> 
                    if _journeyInSession then
                        let query = String.Join("&", ctx.request.query |> List.map(fun (a, b) -> String.Format("{0}={1}", a, defaultArg b String.Empty)))
                        _journeyInSession <- false
                        OK ("You reached the final point. I'll reply everithing that you send me via query string ;)</br>Query: " + query) ctx
                    else
                        OK "Sorry but you have to follow all the Journey again before to accept your data! <a href='/composed/test11/'>Click here to start</a>" ctx
            ]

            POST >=> choose [
                path "/composed/test11/validate" >=> fun (ctx: HttpContext) ->
                    match ctx.request.formData "code" with
                    | Choice1Of2 code when code = "31337" -> 
                        _journeyInSession <- true
                        OK "Gr8 you are a very 31337 hacker. <a href='/composed/test11/final?txt=Hello Hacker'>Go on to a vulnerable page</a>" ctx
                    | Choice1Of2 code -> OK ("Sorry code '" + code + "' is not correct, <a href='javascript:history.back();'>try again</a>") ctx
                    | _ -> OK ("Sorry no code received, <a href='javascript:history.back();'>try again</a>") ctx
            ]
        ]   

