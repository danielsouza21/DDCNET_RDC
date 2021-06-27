oficial: 
	mcs -out:dcc023c2.exe Dcc023c2.cs

all:
	mcs -out:dcc023c2.exe Dcc023c2Error.cs

clean:
	-rm -f dcc023c2.exe
